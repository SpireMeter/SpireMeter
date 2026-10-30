using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using SpireMeter.Core;

namespace SpireMeter.UI;

public sealed class DrawOddsPanel
{
	private const int FontSize = 16;

	private const float Gap = 12f;

	private readonly PanelContainer _panel;

	private readonly Label _label;

	private Control? _anchor;

	private Func<DrawOdds.Result?>? _read;

	private bool _readErrorLogged;

	public static DrawOddsPanel? Instance { get; set; }

	public DrawOddsPanel(CanvasLayer layer)
	{
		_panel = new PanelContainer
		{
			Name = "DrawOddsPanel",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.08f, 0.85f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 6f,
			ContentMarginBottom = 6f
		});
		layer.AddChild(_panel, forceReadableName: false, Node.InternalMode.Disabled);
		_label = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_label.AddThemeFontSizeOverride("font_size", 16);
		_label.AddThemeColorOverride("font_color", Colors.White);
		_label.AddThemeColorOverride("font_outline_color", Colors.Black);
		_label.AddThemeConstantOverride("outline_size", 4);
		_panel.AddChild(_label, forceReadableName: false, Node.InternalMode.Disabled);
		Timer timer = new Timer
		{
			WaitTime = 0.25,
			Autostart = true
		};
		timer.Timeout += Update;
		layer.AddChild(timer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	public void Show(Control anchor, Func<DrawOdds.Result?> read)
	{
		_anchor = anchor;
		_read = read;
		Update();
	}

	public void Hide()
	{
		_anchor = null;
		_read = null;
		_panel.Visible = false;
	}

	private void Update()
	{
		if (_anchor == null || _read == null)
		{
			return;
		}
		if (GodotObject.IsInstanceValid(_anchor) && _anchor.IsVisibleInTree())
		{
			DrawOdds.Result result = ReadSafely();
			if ((object)result != null)
			{
				_label.Text = result.Text;
				_panel.ResetSize();
				_panel.Position = _anchor.GlobalPosition + new Vector2(_anchor.Size.X + 12f, (_anchor.Size.Y - _panel.Size.Y) / 2f);
				_panel.Visible = true;
				return;
			}
		}
		Hide();
	}

	private DrawOdds.Result? ReadSafely()
	{
		try
		{
			return _read?.Invoke();
		}
		catch (Exception value)
		{
			if (!_readErrorLogged)
			{
				_readErrorLogged = true;
				Log.Error($"[{"SpireMeter"}] topdeck chance failed (further errors are not logged): {value}");
			}
			return null;
		}
	}
}
