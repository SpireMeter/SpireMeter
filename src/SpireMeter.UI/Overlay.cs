using Godot;

namespace SpireMeter.UI;

public static class Overlay
{
	public const string LayerName = "SpireMeterOverlay";

	public static CanvasLayer? Attach(SceneTree tree)
	{
		Window root = tree.Root;
		if (root.GetNodeOrNull("SpireMeterOverlay") != null)
		{
			return null;
		}
		CanvasLayer canvasLayer = new CanvasLayer
		{
			Name = "SpireMeterOverlay",
			Layer = 100
		};
		root.CallDeferred(Node.MethodName.AddChild, canvasLayer);
		return canvasLayer;
	}
}
