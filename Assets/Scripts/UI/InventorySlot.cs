using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ячейка сетки или слот надетого.
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler
{
    public ItemStack Stack { get; private set; }
    public string Slot { get; private set; }
    public int Index { get; private set; }
    InventoryWindow owner;
    Image back;
    Text label;
    Text count;

    public static InventorySlot Create(Transform parent, Vector2 position, InventoryWindow owner, int index, string slot)
    {
        var img = UIFactory.Image("Ячейка", parent, new Color(0.18f, 0.16f, 0.14f));
        UIFactory.Place(img.rectTransform, new Vector2(0f, 1f), position, new Vector2(96f, 96f));
        var view = img.gameObject.AddComponent<InventorySlot>();
        view.owner = owner;
        view.Index = index;
        view.Slot = slot;
        view.back = img;
        view.label = UIFactory.Text("Имя", img.transform, "", 15, TextAnchor.MiddleCenter);
        var r = UIFactory.Stretch(view.label.rectTransform);
        r.offsetMin = new Vector2(4f, 14f);
        r.offsetMax = new Vector2(-4f, -4f);
        view.count = UIFactory.Text("Количество", img.transform, "", 16, TextAnchor.LowerRight);
        UIFactory.Stretch(view.count.rectTransform).offsetMax = new Vector2(-6f, 0f);
        return view;
    }

    public void Set(ItemStack stack)
    {
        Stack = stack;
        label.text = stack != null ? InventoryWindow.ShortName(stack) : "";
        count.text = stack != null && stack.count > 1 ? $"×{stack.count}" : stack?.dur >= 0f ? $"{stack.dur:0}" : "";
        // Цвет ячейки по виду предмета, чтобы еду, лекарства и оружие было видно издалека.
        back.color = stack?.Def == null ? new Color(0.18f, 0.16f, 0.14f) : stack.Def.type switch
        {
            "food" or "drink" => new Color(0.24f, 0.3f, 0.18f),
            "medical" => new Color(0.32f, 0.16f, 0.16f),
            "weapon" or "gun" or "ammo" => new Color(0.26f, 0.26f, 0.3f),
            "clothing" => new Color(0.22f, 0.24f, 0.32f),
            "book" or "note" => new Color(0.3f, 0.26f, 0.16f),
            _ => new Color(0.24f, 0.22f, 0.2f),
        };
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (Stack == null)
            return;
        if (e.button == PointerEventData.InputButton.Right)
            ItemActions.Use(Stack, FindAnyObjectByType<PlayerNeeds>());
        else
            owner.Select(Stack);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (Stack != null)
            owner.Select(Stack);
    }

    public void OnBeginDrag(PointerEventData e) => owner.BeginDrag(this, e);
    public void OnDrag(PointerEventData e) => owner.Drag(e);
    public void OnEndDrag(PointerEventData e) => owner.EndDrag(e);
}
