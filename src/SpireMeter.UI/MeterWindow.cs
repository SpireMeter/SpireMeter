using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.UI;

public sealed class MeterWindow
{
	private enum View
	{
		Run,
		Fight,
		Details,
		Debuffs,
		Block,
		Turns
	}

	private const float MinWidth = 240f;

	private const float MinContentHeight = 60f;

	private const float MaxContentHeight = 560f;

	/// <summary>Auto height stops growing here; longer lists scroll.</summary>
	private const float FitCap = 440f;

	/// <summary>Rough characters that fit on one 13px muted line.</summary>
	private const int SubLineChars = 40;

	/// <summary>Height that fits the current view's content, up to <see cref="FitCap"/>.</summary>
	private float _fitHeight;

	/// <summary>Details: players the viewer opened (true) or folded (false); others use the default.</summary>
	private readonly Dictionary<ulong, bool> _open = new Dictionary<ulong, bool>();

	/// <summary>Stacked mode shows the chosen sections one below the other instead of one tab at a time.</summary>
	private bool _stackedMode;

	/// <summary>Stacked mode: every section in the order the player set, top to bottom.</summary>
	private List<View> _stackOrder = new List<View>();

	/// <summary>Stacked mode: sections the player switched off.</summary>
	private readonly HashSet<View> _stackHidden = new HashSet<View>();

	/// <summary>Shown while unlocked: the Tabs / Stacked switch.</summary>
	private readonly HBoxContainer _layoutBar;

	private readonly Button _tabsModeButton;

	private readonly Button _stackedModeButton;

	/// <summary>Shown while unlocked: the Report a bug button.</summary>
	private readonly HBoxContainer _toolsBar;

	/// <summary>Shown when a newer release is out.</summary>
	private readonly Button _updateButton;

	/// <summary>Shown while unlocked in stacked mode: which sections are on, and their order.</summary>
	private readonly VBoxContainer _arrangeBox;

	private bool _legendShown;

	private static readonly Color Muted = new Color(0.75f, 0.75f, 0.75f);

	private static readonly Color Accent = new Color(1f, 0.82f, 0.35f);

	private static readonly Color BarColor = new Color(0.85f, 0.3f, 0.25f);

	private static readonly Color BlockedColor = new Color(0.45f, 0.6f, 0.8f);

	private static readonly Color PoisonColor = new Color(0.45f, 0.8f, 0.3f);

	private static readonly Color DoomColor = new Color(0.62f, 0.4f, 0.9f);

	private const string PoisonLabel = "Poison (shared)";

	private static readonly Color Dim = new Color(1f, 1f, 1f, 0.3f);

	/// <summary>Block a player gave to teammates.</summary>
	private static readonly Color PartyBlockColor = new Color(0.4f, 0.85f, 0.85f);

	/// <summary>Total and fight share one grid: damage, share.</summary>
	private static readonly float[] PartyCols = new float[2] { 56f, 48f };

	/// <summary>Turns, run pace: turns, damage per turn, damage per fight.</summary>
	private static readonly float[] RunPaceCols = new float[3] { 46f, 66f, 72f };

	/// <summary>Turns, average turn: damage, energy, played, drawn.</summary>
	private static readonly float[] TurnCols = new float[4] { 44f, 52f, 52f, 48f };

	/// <summary>Debuffs per source: stacks.</summary>
	private static readonly float[] StackCols = new float[1] { 60f };

	/// <summary>Block per source: block, the part given to teammates.</summary>
	private static readonly float[] BlockCols = new float[2] { 56f, 52f };

	/// <summary>Row backgrounds, shared by every row: one new style per row made long tables slow to build.</summary>
	private static readonly StyleBoxFlat StripeRow = RowBox(new Color(1f, 1f, 1f, 0.05f));

	private static readonly StyleBoxFlat PlainRow = RowBox(new Color(0f, 0f, 0f, 0f));

	/// <summary>Views whose too-wide row was already logged, so the log is written once, not on every refresh.</summary>
	private readonly HashSet<View> _wideLogged = new HashSet<View>();

	private readonly VBoxContainer _content;

	private readonly ScrollContainer _scroll;

	private readonly Label _title;

	private readonly Dictionary<View, Button> _viewButtons = new Dictionary<View, Button>();

	private readonly HBoxContainer _viewBar;

	private readonly Button _collapseButton;

	private static readonly View[] AllViews = Enum.GetValues<View>();

	private readonly MeterSettings _settings = MeterSettings.Load();

	private View _view;

	private readonly PanelContainer _panel;

	private readonly StyleBoxFlat _panelStyle;

	private readonly Button _lockButton;

	private readonly ColorRect _grip;

	private float _width;

	private float? _contentHeight;

	/// <summary>While locked the window ignores drags, so a stray click can't move or resize it.</summary>
	private bool _locked = true;

	private bool _resizing;

	private float _chromeHeight;

	private bool _dragging;

	private Vector2 _grabOffset;

	private bool _needsClamp;

	private bool _collapsed;

	private long _shownVersion = -1L;

	private bool _dirty = true;

	public MeterWindow(CanvasLayer layer)
	{
		PanelContainer panelContainer = new PanelContainer
		{
			Name = "MeterWindow",
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.08f, 0.78f),
			BorderColor = Accent,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 6f,
			ContentMarginBottom = 8f
		};
		panelContainer.AddThemeStyleboxOverride("panel", _panelStyle);
		_width = Mathf.Max(MinWidth, _settings.Width);
		_contentHeight = _settings.ContentHeight.HasValue ? new float?(Mathf.Max(MinContentHeight, _settings.ContentHeight.Value)) : null;
		panelContainer.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		panelContainer.GrowHorizontal = Control.GrowDirection.Begin;
		panelContainer.OffsetRight = -12f;
		panelContainer.OffsetLeft = -12f - _width;
		panelContainer.OffsetTop = 90f;
		panelContainer.CustomMinimumSize = new Vector2(_width, 0f);
		_panel = panelContainer;
		_view = Enum.TryParse<View>(_settings.View, out var savedView) ? savedView : View.Run;
		if (_settings.X.HasValue && _settings.Y.HasValue)
		{
			UseAbsolutePosition(new Vector2(_settings.X.Value, _settings.Y.Value));
			_needsClamp = true;
		}
		layer.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		panelContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.Arrow
		};
		hBoxContainer.GuiInput += OnHeaderInput;
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		_title = MakeLabel("SpireMeter", 17, Accent);
		_title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hBoxContainer.AddChild(_title, forceReadableName: false, Node.InternalMode.Disabled);
		// Hidden until the launch check finds a newer release; a click opens its download page.
		_updateButton = MakeButton("Update", delegate
		{
			OS.ShellOpen(Community.ReleasesUrl);
		});
		_updateButton.AddThemeFontSizeOverride("font_size", 13);
		_updateButton.AddThemeStyleboxOverride("normal", ToggleBox(new Color(Accent, 0.95f)));
		_updateButton.AddThemeStyleboxOverride("hover", ToggleBox(new Color(1f, 0.9f, 0.55f, 1f)));
		_updateButton.AddThemeStyleboxOverride("pressed", ToggleBox(new Color(1f, 0.9f, 0.55f, 1f)));
		_updateButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		foreach (string state in new string[3] { "font_color", "font_hover_color", "font_pressed_color" })
		{
			_updateButton.AddThemeColorOverride(state, new Color(0.1f, 0.07f, 0.01f));
		}
		_updateButton.Visible = false;
		hBoxContainer.AddChild(_updateButton, forceReadableName: false, Node.InternalMode.Disabled);
		_lockButton = MakeButton("Unlock", ToggleLocked);
		_lockButton.ToggleMode = true;
		_lockButton.TooltipText = "Unlock to move (drag the title), resize (drag the corner) and pick the layout: tabs, or sections stacked one below the other";
		StyleToggle(_lockButton);
		hBoxContainer.AddChild(_lockButton, forceReadableName: false, Node.InternalMode.Disabled);
		_collapseButton = MakeButton("-", ToggleCollapsed);
		hBoxContainer.AddChild(_collapseButton, forceReadableName: false, Node.InternalMode.Disabled);
		_layoutBar = new HBoxContainer();
		_layoutBar.AddThemeConstantOverride("separation", 4);
		Label layoutLabel = MakeLabel("Layout", 13, Muted);
		layoutLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		layoutLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		_layoutBar.AddChild(layoutLabel, forceReadableName: false, Node.InternalMode.Disabled);
		_tabsModeButton = MakeButton("Tabs", delegate
		{
			SetStackedMode(stacked: false);
		});
		_stackedModeButton = MakeButton("Stacked", delegate
		{
			SetStackedMode(stacked: true);
		});
		foreach (Button modeButton in new Button[2] { _tabsModeButton, _stackedModeButton })
		{
			modeButton.ToggleMode = true;
			modeButton.AddThemeFontSizeOverride("font_size", 13);
			StyleToggle(modeButton);
			_layoutBar.AddChild(modeButton, forceReadableName: false, Node.InternalMode.Disabled);
		}
		vBoxContainer.AddChild(_layoutBar, forceReadableName: false, Node.InternalMode.Disabled);
		_toolsBar = new HBoxContainer();
		Button report = MakeButton("Report a bug", Community.ReportBug);
		report.AddThemeFontSizeOverride("font_size", 13);
		report.TooltipText = "Saves bug-report.txt (your version and this session's meter log) and shows it, then opens the bug form on GitHub. Drag the file into the form.";
		report.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		report.ClipText = true;
		StyleToggle(report);
		_toolsBar.AddChild(report, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(_toolsBar, forceReadableName: false, Node.InternalMode.Disabled);
		_arrangeBox = new VBoxContainer();
		_arrangeBox.AddThemeConstantOverride("separation", 2);
		vBoxContainer.AddChild(_arrangeBox, forceReadableName: false, Node.InternalMode.Disabled);
		_viewBar = new HBoxContainer();
		vBoxContainer.AddChild(_viewBar, forceReadableName: false, Node.InternalMode.Disabled);
		foreach (View view in AllViews)
		{
			Button button = MakeButton(view.ToString(), delegate
			{
				SelectView(view);
			});
			button.ToggleMode = true;
			button.AddThemeFontSizeOverride("font_size", 14);
			StyleToggle(button);
			// Six tabs share the bar: slim padding, and clipped text so a narrow window never grows to fit them.
			foreach (string state in new string[4] { "normal", "hover", "pressed", "hover_pressed" })
			{
				if (button.GetThemeStylebox(state) is StyleBoxFlat box)
				{
					box.ContentMarginLeft = 3f;
					box.ContentMarginRight = 3f;
				}
			}
			button.ClipText = true;
			button.CustomMinimumSize = new Vector2(0f, 0f);
			button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_viewButtons[view] = button;
			_viewBar.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		}
		LoadLayout();
		_scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
			VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
			CustomMinimumSize = new Vector2(0f, 0f)
		};
		vBoxContainer.AddChild(_scroll, forceReadableName: false, Node.InternalMode.Disabled);
		_content = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_content.AddThemeConstantOverride("separation", 2);
		_scroll.AddChild(_content, forceReadableName: false, Node.InternalMode.Disabled);
		_grip = new ColorRect
		{
			Color = Accent,
			CustomMinimumSize = new Vector2(18f, 12f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.Fdiagsize,
			TooltipText = "Drag to resize, double-click to reset to automatic size",
			Visible = false
		};
		_grip.GuiInput += OnGripInput;
		vBoxContainer.AddChild(_grip, forceReadableName: false, Node.InternalMode.Disabled);
		Timer timer = new Timer
		{
			WaitTime = 0.25,
			Autostart = true
		};
		timer.Timeout += Refresh;
		layer.AddChild(timer, forceReadableName: false, Node.InternalMode.Disabled);
		UpdateViewButtons();
		SetLocked(locked: true);
		if (_settings.Collapsed)
		{
			ToggleCollapsed();
		}
		if (_settings.CheckForUpdates)
		{
			Community.CheckForUpdate();
		}
	}

	private void ShowUpdate(string version)
	{
		_updateButton.Text = "Update " + version;
		_updateButton.TooltipText = $"SpireMeter {version} is out (you have {ModEntry.Version}). Click to open the download page.";
		_updateButton.Visible = true;
	}

	/// <summary>Anchors the window by its top-left corner so it can be placed anywhere on screen.</summary>
	private void UseAbsolutePosition(Vector2 position)
	{
		_panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_panel.GrowHorizontal = Control.GrowDirection.End;
		_panel.OffsetLeft = position.X;
		_panel.OffsetTop = position.Y;
		_panel.OffsetRight = position.X + _width;
		_panel.CustomMinimumSize = new Vector2(_width, 0f);
	}

	private void ToggleLocked()
	{
		SetLocked(!_lockButton.ButtonPressed);
	}

	private void SetLocked(bool locked)
	{
		_locked = locked;
		_dragging = false;
		_resizing = false;
		_lockButton.SetPressedNoSignal(!locked);
		_lockButton.Text = (locked ? "Unlock" : "Lock");
		_panelStyle.BorderWidthLeft = (_panelStyle.BorderWidthRight = (_panelStyle.BorderWidthTop = (_panelStyle.BorderWidthBottom = (locked ? 0 : 2))));
		((Control)_lockButton.GetParent()).MouseDefaultCursorShape = (locked ? Control.CursorShape.Arrow : Control.CursorShape.Move);
		((Control)_lockButton.GetParent()).TooltipText = (locked ? "" : "Drag to move");
		UpdateChrome();
		_dirty = true;
	}

	private static View? ParseView(string? text)
	{
		return (Enum.TryParse<View>(text, out var view) && Enum.IsDefined(view)) ? view : null;
	}

	private void LoadLayout()
	{
		_stackedMode = _settings.Layout == "Stacked";
		_stackOrder = (_settings.StackOrder ?? new List<string>()).Select(ParseView).OfType<View>().Distinct().ToList();
		_stackOrder.AddRange(AllViews.Where((View v) => !_stackOrder.Contains(v)));
		_stackHidden.Clear();
		foreach (View view in _settings.StackHidden.Select(ParseView).OfType<View>())
		{
			_stackHidden.Add(view);
		}
		// At least one section stays on, or stacked mode would be an empty window.
		if (_stackHidden.Count >= AllViews.Length)
		{
			_stackHidden.Clear();
		}
	}

	private void SaveLayout()
	{
		_settings.Layout = _stackedMode ? "Stacked" : "Tabs";
		_settings.StackOrder = _stackOrder.Select((View v) => v.ToString()).ToList();
		_settings.StackHidden = _stackHidden.Select((View v) => v.ToString()).ToList();
		_settings.Save();
	}

	private IEnumerable<View> StackedViews()
	{
		return _stackOrder.Where((View v) => !_stackHidden.Contains(v));
	}

	private void SetStackedMode(bool stacked)
	{
		_stackedMode = stacked;
		SaveLayout();
		UpdateChrome();
		_dirty = true;
		Refresh();
	}

	/// <summary>Shows the bars that fit the mode: tabs only in tab mode, the layout controls only while unlocked.</summary>
	private void UpdateChrome()
	{
		_tabsModeButton.SetPressedNoSignal(!_stackedMode);
		_stackedModeButton.SetPressedNoSignal(_stackedMode);
		_layoutBar.Visible = !_locked && !_collapsed;
		_toolsBar.Visible = !_locked && !_collapsed;
		_viewBar.Visible = !_stackedMode && !_collapsed;
		_arrangeBox.Visible = !_locked && !_collapsed && _stackedMode;
		_grip.Visible = !_locked && !_collapsed;
		if (_arrangeBox.Visible)
		{
			RebuildArrange();
		}
	}

	/// <summary>One row per section: a toggle to show it, and buttons to move it up or down the stack.</summary>
	private void RebuildArrange()
	{
		foreach (Node child in _arrangeBox.GetChildren())
		{
			_arrangeBox.RemoveChild(child);
			child.QueueFree();
		}
		for (int i = 0; i < _stackOrder.Count; i++)
		{
			View view = _stackOrder[i];
			int at = i;
			HBoxContainer row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);
			Button show = MakeButton(view.ToString(), delegate
			{
				ChangeLayout(delegate
				{
					ToggleSection(view);
				});
			});
			show.ToggleMode = true;
			show.AddThemeFontSizeOverride("font_size", 13);
			StyleToggle(show);
			show.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			show.SetPressedNoSignal(!_stackHidden.Contains(view));
			show.TooltipText = "Show or hide this section";
			row.AddChild(show, forceReadableName: false, Node.InternalMode.Disabled);
			row.AddChild(ArrangeButton("Up", at > 0, delegate
			{
				MoveSection(at, -1);
			}), forceReadableName: false, Node.InternalMode.Disabled);
			row.AddChild(ArrangeButton("Down", at < _stackOrder.Count - 1, delegate
			{
				MoveSection(at, 1);
			}), forceReadableName: false, Node.InternalMode.Disabled);
			_arrangeBox.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private Button ArrangeButton(string text, bool enabled, Action onPressed)
	{
		Button button = MakeButton(text, delegate
		{
			ChangeLayout(onPressed);
		});
		button.AddThemeFontSizeOverride("font_size", 13);
		button.CustomMinimumSize = new Vector2(50f, 0f);
		StyleToggle(button);
		button.AddThemeStyleboxOverride("disabled", ToggleBox(new Color(1f, 1f, 1f, 0.03f)));
		button.AddThemeColorOverride("font_disabled_color", Dim);
		button.Disabled = !enabled;
		return button;
	}

	/// <summary>Applies a change from an arrange button after its click, since the rebuild frees that button.</summary>
	private void ChangeLayout(Action change)
	{
		Callable.From(delegate
		{
			change();
			SaveLayout();
			RebuildArrange();
			_dirty = true;
			Refresh();
		}).CallDeferred();
	}

	private void ToggleSection(View view)
	{
		if (!_stackHidden.Remove(view) && StackedViews().Count() > 1)
		{
			_stackHidden.Add(view);
		}
	}

	private void MoveSection(int index, int step)
	{
		int to = index + step;
		if (to >= 0 && to < _stackOrder.Count)
		{
			(_stackOrder[index], _stackOrder[to]) = (_stackOrder[to], _stackOrder[index]);
		}
	}

	private void OnGripInput(InputEvent input)
	{
		if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
		{
			if (button.Pressed && !_locked)
			{
				if (button.DoubleClick)
				{
					_width = 360f;
					_contentHeight = null;
					_fitHeight = 0f;
					_settings.Width = _width;
					_settings.ContentHeight = null;
					_settings.Save();
					UseAbsolutePosition(_panel.GlobalPosition);
					_dirty = true;
					Refresh();
					return;
				}
				UseAbsolutePosition(_panel.GlobalPosition);
				_chromeHeight = _panel.Size.Y - _scroll.Size.Y;
				_resizing = true;
			}
			else if (_resizing)
			{
				_resizing = false;
				_settings.Width = _width;
				_settings.ContentHeight = _contentHeight;
				_settings.Save();
			}
		}
		else if (_resizing && input is InputEventMouseMotion motion)
		{
			Vector2 topLeft = _panel.GlobalPosition;
			Vector2 viewport = _panel.GetViewportRect().Size;
			_width = Mathf.Clamp(motion.GlobalPosition.X - topLeft.X, MinWidth, Mathf.Max(MinWidth, viewport.X - topLeft.X));
			_contentHeight = Mathf.Clamp(motion.GlobalPosition.Y - topLeft.Y - _chromeHeight, MinContentHeight, Mathf.Max(MinContentHeight, viewport.Y - topLeft.Y - _chromeHeight));
			UseAbsolutePosition(topLeft);
			_scroll.CustomMinimumSize = new Vector2(0f, _contentHeight.Value);
		}
	}

	/// <summary>Toggle buttons that make it obvious which sections are on: filled gold when on, dim when off.</summary>
	private static void StyleToggle(Button button)
	{
		StyleBoxFlat off = ToggleBox(new Color(1f, 1f, 1f, 0.07f));
		StyleBoxFlat offHover = ToggleBox(new Color(1f, 1f, 1f, 0.16f));
		StyleBoxFlat on = ToggleBox(new Color(Accent, 0.95f));
		StyleBoxFlat onHover = ToggleBox(new Color(1f, 0.9f, 0.55f, 1f));
		Color darkText = new Color(0.1f, 0.07f, 0.01f);
		button.AddThemeStyleboxOverride("normal", off);
		button.AddThemeStyleboxOverride("hover", offHover);
		button.AddThemeStyleboxOverride("pressed", on);
		button.AddThemeStyleboxOverride("hover_pressed", onHover);
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeColorOverride("font_color", Muted);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", darkText);
		button.AddThemeColorOverride("font_hover_pressed_color", darkText);
	}

	private static StyleBoxFlat ToggleBox(Color color)
	{
		return new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 8f,
			ContentMarginRight = 8f,
			ContentMarginTop = 3f,
			ContentMarginBottom = 3f
		};
	}

	private void OnHeaderInput(InputEvent input)
	{
		if (_locked)
		{
			return;
		}
		if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
		{
			if (button.Pressed)
			{
				Vector2 globalPosition = _panel.GlobalPosition;
				UseAbsolutePosition(globalPosition);
				_grabOffset = button.GlobalPosition - globalPosition;
				_dragging = true;
			}
			else if (_dragging)
			{
				_dragging = false;
				_settings.X = _panel.GlobalPosition.X;
				_settings.Y = _panel.GlobalPosition.Y;
				_settings.Save();
			}
		}
		else if (_dragging && input is InputEventMouseMotion motion)
		{
			MoveTo(motion.GlobalPosition - _grabOffset);
		}
	}

	/// <summary>Moves the window, keeping it fully on screen so it can never be dragged out of reach.</summary>
	private void MoveTo(Vector2 position)
	{
		Vector2 size = _panel.GetViewportRect().Size - _panel.Size;
		UseAbsolutePosition(new Vector2(Mathf.Clamp(position.X, 0f, Mathf.Max(0f, size.X)), Mathf.Clamp(position.Y, 0f, Mathf.Max(0f, size.Y))));
	}

	private void SelectView(View view)
	{
		_view = view;
		_settings.View = view.ToString();
		_settings.Save();
		_dirty = true;
		UpdateViewButtons();
		Refresh();
	}

	private void ToggleCollapsed()
	{
		_collapsed = !_collapsed;
		_collapseButton.Text = (_collapsed ? "+" : "-");
		_scroll.Visible = !_collapsed;
		UpdateChrome();
		if (_settings.Collapsed != _collapsed)
		{
			_settings.Collapsed = _collapsed;
			_settings.Save();
		}
		_dirty = true;
		Refresh();
	}

	private void UpdateViewButtons()
	{
		foreach (var (view2, button2) in _viewButtons)
		{
			button2.SetPressedNoSignal(view2 == _view);
		}
	}

	private void Refresh()
	{
		if (_needsClamp && _panel.IsInsideTree())
		{
			// A saved position from a bigger screen or window must not leave the meter off-screen.
			_needsClamp = false;
			MoveTo(_panel.GlobalPosition);
		}
		if (!_updateButton.Visible && Community.NewerVersion is string newer)
		{
			ShowUpdate(newer);
		}
		MeterState state = MeterRecorder.State;
		if (!_dirty && state.Version == _shownVersion)
		{
			return;
		}
		_dirty = false;
		_shownVersion = state.Version;
		MeterSnapshot meterSnapshot = state.Snapshot();
		_title.Text = ((_collapsed && meterSnapshot.TotalDamage > 0) ? $"SpireMeter · {meterSnapshot.TotalDamage}" : "SpireMeter");
		if (_collapsed)
		{
			return;
		}
		foreach (Node child in _content.GetChildren())
		{
			_content.RemoveChild(child);
			child.QueueFree();
		}
		_legendShown = false;
		int sections = 1;
		if (_stackedMode)
		{
			// Stacked: every section that is switched on, one below the other, each under its own title.
			List<View> views = StackedViews().ToList();
			sections = views.Count;
			for (int i = 0; i < views.Count; i++)
			{
				AddStackTitle(views[i], i == 0);
				Build(views[i], meterSnapshot);
			}
		}
		else
		{
			Build(_view, meterSnapshot);
		}
		LogWideContent();
		// Fit the content: the window grows with the number of players and rows, up to the screen.
		// Each stacked section gets its own share of room before the list starts to scroll.
		float y = _content.GetCombinedMinimumSize().Y;
		_fitHeight = Mathf.Min(y, Mathf.Min(AvailableContentHeight(), FitCap * sections));
		_scroll.CustomMinimumSize = new Vector2(0f, _contentHeight ?? _fitHeight);
	}

	private void Build(View view, MeterSnapshot s)
	{
		switch (view)
		{
		case View.Run:
			BuildRun(s);
			break;
		case View.Fight:
			BuildFight(s);
			break;
		case View.Details:
			BuildDetails(s);
			break;
		case View.Debuffs:
			BuildDebuffs(s);
			break;
		case View.Block:
			BuildBlock(s);
			break;
		case View.Turns:
			BuildTurns(s);
			break;
		}
	}

	/// <summary>The title of a stacked section, in the same quiet style as the other section headers.</summary>
	private void AddStackTitle(View view, bool first)
	{
		if (!first)
		{
			_content.AddChild(new Control
			{
				CustomMinimumSize = new Vector2(0f, 6f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, forceReadableName: false, Node.InternalMode.Disabled);
			AddDivider();
		}
		_content.AddChild(MakeLabel(view.ToString().ToUpperInvariant(), 13, Accent), forceReadableName: false, Node.InternalMode.Disabled);
	}

	/// <summary>Records which row is wider than the window, so a stretching layout can be traced.</summary>
	private void LogWideContent()
	{
		if (_wideLogged.Contains(_view))
		{
			return;
		}
		try
		{
			Control? widest = null;
			float max = 0f;
			foreach (Node node in _content.GetChildren())
			{
				if (node is Control control && control.GetCombinedMinimumSize().X > max)
				{
					max = control.GetCombinedMinimumSize().X;
					widest = control;
				}
			}
			if (max > _width - 30f)
			{
				string text = (widest?.GetChildren().OfType<Label>().FirstOrDefault()?.Text) ?? (widest as Label)?.Text ?? "";
				System.IO.File.AppendAllText(ProjectSettings.GlobalizePath("user://SpireMeter/layout.log"), $"{DateTime.Now:s} view={_view} width={_width} widest={max} type={widest?.GetType().Name} first=\"{text}\"\n");
				_wideLogged.Add(_view);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Room left below the window's top edge, so a long list scrolls instead of running off screen.</summary>
	private float AvailableContentHeight()
	{
		if (!_panel.IsInsideTree())
		{
			return MaxContentHeight;
		}
		return Mathf.Clamp(_panel.GetViewportRect().Size.Y - _panel.GlobalPosition.Y - 120f, 160f, 900f);
	}

	private void AddDivider()
	{
		// Drawn by hand: the theme's HSeparator is not reliably visible on this overlay.
		_content.AddChild(new ColorRect
		{
			Color = new Color(1f, 1f, 1f, 0.3f),
			CustomMinimumSize = new Vector2(0f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void BuildRun(MeterSnapshot s)
	{
		if (s.Players.Count == 0)
		{
			AddNote("No run yet. Numbers appear in the first fight.");
			return;
		}
		// The damage meter: party damage on top, then who dealt it. Debuffs and block have their own tabs.
		int fights = s.FightsFinished + (s.InCombat ? 1 : 0);
		AddCols($"Party damage · run, {fights} fight{((fights == 1) ? "" : "s")}", new string[2] { Num(s.TotalDamage), "" }, PartyCols, 16, Accent, new Color[2] { Accent, Accent }, stripe: true);
		bool compare = s.Players.Count > 1;
		long bestDamage = s.Players.Max((PlayerTotals p) => p.Damage);
		foreach (PlayerTotals player in s.Players)
		{
			AddCols(PlayerLabel(player.Player), new string[2]
			{
				Num(player.Damage),
				Pct(player.Damage, s.TotalDamage)
			}, PartyCols, 16, null, new Color[2]
			{
				Highlight(player.Damage, bestDamage, compare),
				Muted
			});
			AddTypeBar(player.Split, bestDamage);
		}
		if (s.PoisonDamage > 0)
		{
			AddCols(PoisonLabel, new string[2] { s.PoisonDamage.ToString(), Pct(s.PoisonDamage, s.TotalDamage) }, PartyCols, 15, PoisonColor, new Color[2] { PoisonColor, Muted });
		}
		if (s.DoomDamage > 0)
		{
			AddCols("Doom (shared)", new string[2] { s.DoomDamage.ToString(), Pct(s.DoomDamage, s.TotalDamage) }, PartyCols, 15, DoomColor, new Color[2] { DoomColor, Muted });
		}
		AddLegend();
	}

	/// <summary>One line naming the bar colors, in the colors themselves.</summary>
	private void AddLegend()
	{
		// Run and Fight stacked together would print it twice.
		if (_legendShown)
		{
			return;
		}
		_legendShown = true;
		HBoxContainer row = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 10);
		foreach (var (text, color) in new (string, Color)[4]
		{
			("HP", BarColor),
			("Block", BlockedColor),
			("Poison", PoisonColor),
			("Doom", DoomColor)
		})
		{
			// A drawn swatch: the game font may not have a square glyph.
			row.AddChild(new ColorRect
			{
				Color = color,
				CustomMinimumSize = new Vector2(9f, 9f),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, forceReadableName: false, Node.InternalMode.Disabled);
			row.AddChild(MakeLabel(text, 12, Muted), forceReadableName: false, Node.InternalMode.Disabled);
		}
		_content.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
	}

	/// <summary>Each character gets its own bar color, so players are easy to tell apart at a glance.</summary>
	private static Color CharacterColor(PlayerRef player)
	{
		string c = player.Character ?? "";
		if (c.Contains("Ironclad", StringComparison.OrdinalIgnoreCase))
		{
			return new Color(0.85f, 0.25f, 0.2f);
		}
		if (c.Contains("Silent", StringComparison.OrdinalIgnoreCase))
		{
			return new Color(0.3f, 0.7f, 0.3f);
		}
		if (c.Contains("Defect", StringComparison.OrdinalIgnoreCase))
		{
			return new Color(0.25f, 0.55f, 0.9f);
		}
		if (c.Contains("Regent", StringComparison.OrdinalIgnoreCase))
		{
			return new Color(0.95f, 0.6f, 0.15f);
		}
		if (c.Contains("Necro", StringComparison.OrdinalIgnoreCase))
		{
			return new Color(0.6f, 0.4f, 0.85f);
		}
		return new Color(0.6f, 0.6f, 0.6f);
	}

	/// <summary>Whole percent, formatted by hand: the game's culture puts a space before % that does not fit the column.</summary>
	private static string Pct(long part, long total)
	{
		return (total == 0L) ? "" : $"{Math.Round(100.0 * (double)part / (double)total)}%";
	}

	/// <summary>All debuff stacks a player applied, poison included.</summary>
	private static long DebuffStacks(PlayerTotals player)
	{
		return player.Debuffs.Sum((NamedAmount d) => d.Amount);
	}

	/// <summary>
	/// The thin bar under a player row, one segment per damage type. Sized like raid damage meters: the top
	/// value fills the width and everyone else is measured against it, so four players never all look small.
	/// </summary>
	private void AddTypeBar(DamageSplit split, long best)
	{
		Control control = new Control
		{
			CustomMinimumSize = new Vector2(0f, 5f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		control.AddChild(BarPart(0.0, 1.0, new Color(1f, 1f, 1f, 0.08f)), forceReadableName: false, Node.InternalMode.Disabled);
		double at = 0.0;
		foreach (var (amount, color) in new (long, Color)[4]
		{
			(split.Hp, BarColor),
			(split.Blocked, BlockedColor),
			(split.Poison, PoisonColor),
			(split.Doom, DoomColor)
		})
		{
			double to = (best <= 0L) ? at : (at + (double)amount / (double)best);
			if (amount > 0L)
			{
				control.AddChild(BarPart(at, to, color), forceReadableName: false, Node.InternalMode.Disabled);
			}
			at = to;
		}
		_content.AddChild(control, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static ColorRect BarPart(double from, double to, Color color)
	{
		return new ColorRect
		{
			Color = color,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = (float)Mathf.Clamp(from, 0.0, 1.0),
			AnchorRight = (float)Mathf.Clamp(to, 0.0, 1.0),
			AnchorTop = 0f,
			AnchorBottom = 1f
		};
	}

	private void BuildFight(MeterSnapshot s)
	{
		if (s.Fight.Count == 0)
		{
			AddNote("No fight yet. This tab shows the current fight, or the last one between fights.");
			return;
		}
		// Per-player fight damage counts hits into block too, like the Run tab.
		IReadOnlyList<PlayerSplit> byPlayer = s.FightPlayers ?? Array.Empty<PlayerSplit>();
		long fightPoison = s.Fight.Sum((EnemyFight f) => f.Poison);
		long fightTotal = byPlayer.Sum((PlayerSplit p) => p.Split.Total) + fightPoison;
		AddCols(s.InCombat ? "Party damage · this fight" : "Party damage · last fight", new string[2] { Num(fightTotal), "" }, PartyCols, 15, Accent, new Color[2] { Accent, Accent }, stripe: true);
		long bestFight = byPlayer.Select((PlayerSplit p) => p.Split.Total).DefaultIfEmpty(0L).Max();
		foreach (PlayerSplit row in byPlayer)
		{
			long amount = row.Split.Total;
			AddCols(PlayerLabel(row.Player), new string[2] { Num(amount), Pct(amount, fightTotal) }, PartyCols, 15, null, new Color[2]
			{
				Highlight(amount, bestFight, byPlayer.Count > 1),
				Muted
			});
			AddTypeBar(row.Split, bestFight);
		}
		if (fightPoison > 0)
		{
			AddCols(PoisonLabel, new string[2] { fightPoison.ToString(), Pct(fightPoison, fightTotal) }, PartyCols, 15, PoisonColor, new Color[2] { PoisonColor, Muted });
		}
		Dictionary<string, int> dictionary = (from f in s.Fight
			group f by f.Enemy.Name).ToDictionary((IGrouping<string, EnemyFight> g) => g.Key, (IGrouping<string, EnemyFight> g) => g.Count());
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		foreach (EnemyFight item in s.Fight)
		{
			string text = item.Enemy.Name;
			if (dictionary[text] > 1)
			{
				dictionary2[text] = dictionary2.GetValueOrDefault(text) + 1;
				text += $" #{dictionary2[text]}";
			}
			// Enemies are context, not the point: one dim line each, damage taken in the same column.
			AddCols("   " + text, new string[2] { Num(item.Total), "" }, PartyCols, 13, Muted, new Color[2] { Muted, Muted });
		}
		AddLegend();
	}

	/// <summary>Damage per source: each player's cards, then their relics, potions and powers.</summary>
	private void BuildDetails(MeterSnapshot s)
	{
		BuildSources(s, new string[2] { "Dmg", "%" }, PartyCols, (CardAmount c) => c.Damage, (PlayerTotals p) => new string[2]
		{
			Num(p.Damage),
			""
		}, (CardAmount c, PlayerTotals p) => new string[2]
		{
			Num(c.Damage),
			Pct(c.Damage, p.Damage)
		}, null, null, null, "Nothing yet. Sources show up once they deal damage.");
	}

	/// <summary>Debuff stacks per source; a source that applies several types gets them spelled out.</summary>
	private void BuildDebuffs(MeterSnapshot s)
	{
		// One debuff type goes right after the source's name; several are spelled out with their stacks below it.
		BuildSources(s, new string[1] { "Stacks" }, StackCols, CardDebuffs, (PlayerTotals p) => new string[1] { Num(DebuffStacks(p)) }, (CardAmount c, PlayerTotals p) => new string[1] { Num(CardDebuffs(c)) }, null, delegate(CardAmount card)
		{
			List<NamedAmount> types = DebuffTypes(card);
			if (types.Count > 1)
			{
				AddSubLine("      " + JoinAmounts(types));
			}
		}, delegate(PlayerTotals player)
		{
			if (player.Debuffs.Count > 0)
			{
				AddSubLine("   " + JoinAmounts(player.Debuffs));
			}
		}, "Nothing yet. Sources show up once they apply debuffs.", delegate(CardAmount card)
		{
			List<NamedAmount> types = DebuffTypes(card);
			return (types.Count == 1) ? types[0].Name : null;
		});
	}

	/// <summary>Every debuff type a source applied, poison included, with its stacks.</summary>
	private static List<NamedAmount> DebuffTypes(CardAmount card)
	{
		List<NamedAmount> types = new List<NamedAmount>();
		if (card.Poison > 0)
		{
			types.Add(new NamedAmount("Poison", card.Poison));
		}
		types.AddRange(card.Debuffs);
		return types;
	}

	/// <summary>Block per source, and the part of it that went to teammates.</summary>
	private void BuildBlock(MeterSnapshot s)
	{
		BuildSources(s, new string[2] { "Block", "Party" }, BlockCols, (CardAmount c) => c.Block, (PlayerTotals p) => new string[2]
		{
			Num(p.BlockGained),
			Num(p.BlockParty)
		}, (CardAmount c, PlayerTotals p) => new string[2]
		{
			Num(c.Block),
			Num(c.BlockParty)
		}, PartyBlockColor, null, null, "Nothing yet. Sources show up once they give block.");
	}

	/// <summary>
	/// One table per player of the sources that count for this tab, biggest first: cards, then relics, potions
	/// and powers. Teammates fold to a single line of totals; click a player's line to open or fold it.
	/// </summary>
	private void BuildSources(MeterSnapshot s, string[] headers, float[] cols, Func<CardAmount, long> amount, Func<PlayerTotals, string[]> totals, Func<CardAmount, PlayerTotals, string[]> values, Color? secondColor, Action<CardAmount>? afterRow, Action<PlayerTotals>? afterTotal, string empty, Func<CardAmount, string?>? tag = null)
	{
		bool any = false;
		int index = 0;
		Color[] headerColors = headers.Select((string _) => Muted).ToArray();
		foreach (PlayerTotals player in s.Players)
		{
			List<CardAmount> sources = player.Cards.Where((CardAmount c) => amount(c) > 0).OrderByDescending(amount).ToList();
			if (sources.Count == 0)
			{
				continue;
			}
			if (any)
			{
				AddDivider();
			}
			any = true;
			ulong id = player.Player.Id;
			if (!IsOpen(id, s.Players.Count, index++))
			{
				Clickable(AddCols("+  " + PlayerLabel(player.Player), totals(player), cols, 15, Accent), delegate
				{
					_open[id] = true;
				});
				continue;
			}
			// The player's line doubles as the column header for their table.
			Clickable(AddCols("-  " + PlayerLabel(player.Player).ToUpperInvariant(), headers, cols, 13, Accent, headerColors), delegate
			{
				_open[id] = false;
			});
			long best = sources.Max(amount);
			bool compare = sources.Count > 1;
			bool stripe = false;
			void Row(CardAmount card, string name)
			{
				string[] cells = values(card, player);
				Color[] colors = new Color[cells.Length];
				colors[0] = Highlight(amount(card), best, compare);
				for (int i = 1; i < cells.Length; i++)
				{
					colors[i] = (cells[i] == "-" || cells[i] == "") ? Dim : (secondColor ?? Muted);
				}
				string? extra = tag?.Invoke(card);
				AddCols(string.IsNullOrEmpty(extra) ? name : (name + " · " + extra), cells, cols, 15, null, colors, stripe);
				stripe = !stripe;
				afterRow?.Invoke(card);
			}
			foreach (CardAmount card in sources.Where((CardAmount c) => c.IsCard))
			{
				Row(card, card.Name);
			}
			List<CardAmount> others = sources.Where((CardAmount c) => !c.IsCard).ToList();
			if (others.Count > 0)
			{
				AddCols("Relics, potions, powers", Array.Empty<string>(), cols, 12, Muted);
				stripe = false;
				foreach (CardAmount card in others)
				{
					Row(card, (card.Kind == "other") ? card.Name : $"{card.Name} ({card.Kind})");
				}
			}
			AddCols("Total", totals(player), cols, 15, Muted);
			afterTotal?.Invoke(player);
		}
		if (!any)
		{
			AddNote(empty);
		}
	}

	/// <summary>Your own player starts open, teammates folded; in single player the first one is open.</summary>
	private bool IsOpen(ulong id, int players, int index)
	{
		if (_open.TryGetValue(id, out bool open))
		{
			return open;
		}
		ulong? local = GameEventMapper.LocalPlayerId();
		return local.HasValue ? (id == local.Value) : (players == 1 || index == 0);
	}

	/// <summary>Makes a row react to a left click; the view is rebuilt after the click, never inside it.</summary>
	private void Clickable(Control row, Action onClick)
	{
		row.MouseFilter = Control.MouseFilterEnum.Stop;
		row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		row.GuiInput += delegate(InputEvent input)
		{
			if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				onClick();
				_dirty = true;
				Callable.From(Refresh).CallDeferred();
			}
		};
	}

	private static long CardDebuffs(CardAmount card)
	{
		return card.Poison + card.Debuffs.Sum((NamedAmount d) => d.Amount);
	}

	private void AddSubLine(string text)
	{
		// Split into fixed rows instead of an auto-wrapping label, which can stretch the window past its width.
		string indent = new string(' ', text.Length - text.TrimStart().Length);
		string line = "";
		foreach (string part in text.TrimStart().Split(" · "))
		{
			if (line.Length > 0 && indent.Length + line.Length + part.Length > SubLineChars)
			{
				AddRow(indent + line, "", "", 13, Muted);
				line = "";
			}
			line += (line.Length > 0 ? " · " : "") + part;
		}
		if (line.Length > 0)
		{
			AddRow(indent + line, "", "", 13, Muted);
		}
	}

	private static string JoinAmounts(IReadOnlyList<NamedAmount> amounts)
	{
		return string.Join(" · ", amounts.Select((NamedAmount a) => $"{a.Name} {a.Amount}"));
	}

	/// <summary>How fast the damage comes and what a turn looks like, per player.</summary>
	private void BuildTurns(MeterSnapshot s)
	{
		if (s.Players.Count == 0 || s.Players.All((PlayerTotals p) => p.TurnsPlayed == 0L))
		{
			AddNote("No fight yet. Pace appears in the first fight.");
			return;
		}
		int fights = s.FightsFinished + (s.InCombat ? 1 : 0);
		int recent = s.Players.Select((PlayerTotals p) => p.Turns.Fights).Max();
		bool compare = s.Players.Count > 1;
		Color[] header3 = new Color[3] { Muted, Muted, Muted };
		// Pace over the whole run: how many turns each player took and how hard they hit per turn and per fight.
		AddCols($"Run · {fights} fight{((fights == 1) ? "" : "s")}", new string[3] { "Turns", "Dmg/turn", "Dmg/fight" }, RunPaceCols, 12, Muted, header3);
		double bestPerTurn = s.Players.Max((PlayerTotals p) => p.DamagePerTurn);
		foreach (PlayerTotals player in s.Players)
		{
			double perFight = (fights == 0) ? 0.0 : ((double)player.Damage / (double)fights);
			AddCols(player.Player.Name, new string[3]
			{
				player.TurnsPlayed.ToString(),
				player.DamagePerTurn.ToString("0.0"),
				perFight.ToString("0")
			}, RunPaceCols, 15, CharacterColor(player.Player).Lightened(0.35f), new Color[3]
			{
				Muted,
				(compare && player.DamagePerTurn == bestPerTurn && bestPerTurn > 0.0) ? Accent : Colors.White,
				Colors.White
			});
		}
		AddDivider();
		// What an average turn looks like lately.
		Color[] header4 = new Color[4] { Muted, Muted, Muted, Muted };
		AddCols($"Per turn · last {recent} fight{((recent == 1) ? "" : "s")}", new string[4] { "Dmg", "Energy", "Played", "Drawn" }, TurnCols, 12, Muted, header4);
		foreach (PlayerTotals player in s.Players)
		{
			TurnAverages turns = player.Turns;
			if (turns.Turns == 0)
			{
				continue;
			}
			AddCols(player.Player.Name, new string[4]
			{
				turns.Damage.ToString("0.0"),
				turns.Energy.ToString("0.0"),
				turns.Played.ToString("0.0"),
				turns.Drawn.ToString("0.0")
			}, TurnCols, 15, CharacterColor(player.Player).Lightened(0.35f), new Color[4] { Colors.White, Colors.White, Colors.White, Colors.White });
		}
		AddNote("Turns: turns each player has taken this run." + (s.InCombat ? " The current fight is included." : ""));
	}

	private static string PlayerLabel(PlayerRef p)
	{
		if (!string.IsNullOrEmpty(p.Character) && !(p.Character == p.Name))
		{
			return p.Name + " (" + p.Character + ")";
		}
		return p.Name;
	}

	private void AddNote(string text)
	{
		// Clipped rows split by hand: an unclipped label is as wide as its text and stretches the window.
		string line = "";
		foreach (string word in text.Split(' '))
		{
			if (line.Length > 0 && line.Length + word.Length + 1 > SubLineChars)
			{
				AddRow(line, "", "", 14, Muted);
				line = "";
			}
			line += (line.Length > 0 ? " " : "") + word;
		}
		if (line.Length > 0)
		{
			AddRow(line, "", "", 14, Muted);
		}
	}

	/// <summary>
	/// One row of the column grid: a clipped name on the left and right-aligned values of fixed width, so every
	/// number lines up under its header and nothing can push the window wider.
	/// </summary>
	private PanelContainer AddCols(string name, IReadOnlyList<string> values, float[] widths, int fontSize, Color? nameColor = null, IReadOnlyList<Color>? valueColors = null, bool stripe = false)
	{
		HBoxContainer row = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 2);
		Label label = MakeLabel(name, fontSize, nameColor ?? Colors.White);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		row.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		for (int i = 0; i < values.Count; i++)
		{
			Label value = MakeLabel(values[i], fontSize, (valueColors != null) ? valueColors[i] : Colors.White);
			value.HorizontalAlignment = HorizontalAlignment.Right;
			value.ClipText = true;
			value.CustomMinimumSize = new Vector2(widths[i], 0f);
			row.AddChild(value, forceReadableName: false, Node.InternalMode.Disabled);
		}
		PanelContainer cell = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		cell.AddThemeStyleboxOverride("panel", stripe ? StripeRow : PlainRow);
		cell.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
		_content.AddChild(cell, forceReadableName: false, Node.InternalMode.Disabled);
		return cell;
	}

	private static StyleBoxFlat RowBox(Color color)
	{
		return new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			ContentMarginLeft = 4f,
			ContentMarginRight = 4f
		};
	}

	/// <summary>Zero shows as a dim dash so the real numbers stand out.</summary>
	private static string Num(long value)
	{
		return (value == 0L) ? "-" : value.ToString();
	}

	/// <summary>Gold for the best value in a column, dim for nothing, white otherwise.</summary>
	private static Color Highlight(long value, long best, bool compare = true)
	{
		if (value == 0L)
		{
			return Dim;
		}
		return (compare && value == best) ? Accent : Colors.White;
	}

	private void AddRow(string name, string value, string extra, int fontSize, Color? nameColor = null, Color? valueColor = null)
	{
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		Label label = MakeLabel(name, fontSize, nameColor ?? Colors.White);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		hBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		Label label2 = MakeLabel(value, fontSize, valueColor ?? Colors.White);
		label2.HorizontalAlignment = HorizontalAlignment.Right;
		hBoxContainer.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		if (extra.Length > 0)
		{
			Label label3 = MakeLabel(extra, fontSize, Muted);
			label3.HorizontalAlignment = HorizontalAlignment.Right;
			label3.CustomMinimumSize = new Vector2(48f, 0f);
			hBoxContainer.AddChild(label3, forceReadableName: false, Node.InternalMode.Disabled);
		}
		_content.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		Label label = new Label();
		label.Text = text;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 4);
		return label;
	}

	private static Button MakeButton(string text, Action onPressed)
	{
		Button button = new Button();
		button.Text = text;
		button.FocusMode = Control.FocusModeEnum.None;
		button.AddThemeFontSizeOverride("font_size", 15);
		button.Pressed += onPressed;
		return button;
	}
}
