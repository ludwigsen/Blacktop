using UnityEngine;
using UnityEngine.UI;

// The shared sprites carry shape and wear. Their callers own layout, tint and interaction.
public static class StreetArt
{
    public static void Apply(Image image, Sprite sprite)
    {
        if (image.sprite == sprite) return;

        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0
            ? Image.Type.Sliced
            : Image.Type.Simple;
    }

    public static Image Layer(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max,
        UIColorRole color, float opacity = 1)
    {
        var layer = FrontEndUI.Panel(parent, name, min, max, Color.clear);
        // Decorative layers must not intercept a pointer or participate in a parent's layout.
        layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var image = layer.GetComponent<Image>();
        image.raycastTarget = false;
        Apply(image, sprite);
        ThemeableUI.Bind(image, color, opacity: opacity);
        image.enabled = sprite != null;
        return image;
    }
}
