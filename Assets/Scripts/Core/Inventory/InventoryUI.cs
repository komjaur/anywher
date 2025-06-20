using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public sealed class InventoryUI : MonoBehaviour
{
    [Header("Slot visuals (optional prefab)")]
    [SerializeField] InventorySlotUI slotPrefab;
    [SerializeField] int             iconSize = 52;
    [SerializeField] Vector2         gridGap  = new(4, 4);

    [Header("Highlight")]
    [SerializeField] Image  highlightFrame;
    [SerializeField] Color  highlightColor = new(1f, 0.92f, 0.016f, 0.4f);
    [SerializeField] Sprite highlightSprite;

    Inventory         inv;
    InventorySlotUI[] slots;

    /* ───────── lifecycle ───────── */
    void Awake()
    {
        // dock 20 px above the bottom-centre of the canvas
        var rt = GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 20);

        gameObject.SetActive(true);           // ensure visible on first frame
    }

    public void Initialise(Inventory inventory)
    {
        inv = inventory;
        BuildGrid();
        inv.OnHotbarChanged += MoveHighlight;
       
    }

    /* ───────── grid construction ───────── */
    void BuildGrid()
    {
        if (inv == null) return;

        int count = inv.Slots.Length;
        slots = new InventorySlotUI[count];

        /* parent grid ---------------------------------------------------- */
        var gridRT = new GameObject("Hotbar", typeof(RectTransform))
                     .GetComponent<RectTransform>();
        gridRT.SetParent(transform, false);

        var grid = gridRT.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(iconSize, iconSize);
        grid.spacing         = gridGap;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 10;

        // let the container auto-resize to its children
        var fitter = gridRT.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        /* slots ---------------------------------------------------------- */
        bool usePrefab = slotPrefab != null;

        for (int i = 0; i < count; ++i)
        {
            InventorySlotUI s = usePrefab
                ? Instantiate(slotPrefab, gridRT)
                : CreateRuntimeSlot(gridRT, $"Slot{i}");
            s.Initialise(inv, i);
            slots[i] = s;
        }

        /* highlight ------------------------------------------------------ */
        if (highlightFrame == null) highlightFrame = CreateRuntimeHighlight();

        highlightFrame.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        MoveHighlight(inv.ActiveHotbarIndex);
    }

    /* ───────── runtime ───────── */
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            gameObject.SetActive(!gameObject.activeSelf);
    }

    void MoveHighlight(int index)
    {
        if (index < 0 || index >= slots.Length) return;

        RectTransform slotRT = (RectTransform)slots[index].transform;
        highlightFrame.transform.SetParent(slotRT, false);
        highlightFrame.transform.SetAsLastSibling();
    }

    void OnDestroy()
    {
        if (inv != null) inv.OnHotbarChanged -= MoveHighlight;
    }

       /* ───────────────────────── factory helpers ───────────────────────── */
    static InventorySlotUI CreateRuntimeSlot(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform),
                                      typeof(Image),
                                      typeof(InventorySlotUI));
        go.transform.SetParent(parent, false);

        var bg = go.GetComponent<Image>();
        bg.color         = new Color(255f, 255f, 255f, 1f);
        bg.raycastTarget = false;

        var countGO = new GameObject("Count", typeof(RectTransform));
        countGO.transform.SetParent(go.transform, false);

        var txt = countGO.AddComponent<TextMeshProUGUI>();
        txt.fontSize         = 14;
        txt.alignment        = TextAlignmentOptions.BottomRight;
        txt.enableAutoSizing = true;

        var txtRT = txt.rectTransform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        return go.GetComponent<InventorySlotUI>();
    }

    Image CreateRuntimeHighlight()
    {
        var hl = new GameObject("Highlight", typeof(RectTransform), typeof(Image))
                 .GetComponent<Image>();
        hl.sprite        = highlightSprite;   // may be null
        hl.color         = highlightColor;
        hl.raycastTarget = false;
        return hl;
    }                                   
}
