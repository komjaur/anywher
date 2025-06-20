/* =========================================================================
 *  CraftingUI — tidy, centred, hover-friendly
 * ========================================================================= */
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Text;

public sealed class CraftingUI : MonoBehaviour
{
    /* ------------ injected refs ------------ */
    CraftingManager crafting;
    Inventory       inv;

    /* ------------ runtime ui refs ------------ */
    CanvasGroup cg;
    Transform   content;                 // parent for recipe cards

    const float PANEL_WIDTH   = 520f;
    const float PANEL_HEIGHT  = 420f;
    const float CARD_HEIGHT   = 54f;
    static readonly Color COL_BG       = new(0.15f, 0.15f, 0.15f, 0.92f);
    static readonly Color COL_CARD     = new(0.24f, 0.24f, 0.24f, 1f);
    static readonly Color COL_CARD_BAD = new(0.12f, 0.12f, 0.12f, 1f);
    static readonly Color COL_CARD_HL  = new(0.32f, 0.32f, 0.32f, 1f);

    /* --------------------------------------------------------------------- */
    public void Initialize(Canvas parentCanvas,
                           CraftingManager craftingMgr,
                           Inventory       playerInventory)
    {
        crafting = craftingMgr;
        inv      = playerInventory;

        /* ---------- Raycasters / EventSystem ---------- */
        if (!parentCanvas.GetComponent<GraphicRaycaster>())
            parentCanvas.gameObject.AddComponent<GraphicRaycaster>();

        if (EventSystem.current == null)
            new GameObject("EventSystem",
                           typeof(EventSystem),
                           typeof(StandaloneInputModule));

        /* ---------- full-screen root ---------- */
        var rootRT = gameObject.AddComponent<RectTransform>();
        rootRT.SetParent(parentCanvas.transform, false);
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;

        cg = gameObject.AddComponent<CanvasGroup>();
        Hide();

        /* ---------- scrim (clicks close UI) ---------- */
        var scrim = new GameObject("Scrim", typeof(RectTransform),
                                             typeof(Image),
                                             typeof(Button));
        scrim.transform.SetParent(transform, false);
        var scrimRT = (RectTransform)scrim.transform;
        scrimRT.anchorMin = Vector2.zero;
        scrimRT.anchorMax = Vector2.one;
        scrimRT.offsetMin = scrimRT.offsetMax = Vector2.zero;
        var scrimImg = scrim.GetComponent<Image>();
        scrimImg.color = new Color(0f, 0f, 0f, 0.65f);
        scrim.GetComponent<Button>().onClick.AddListener(Hide);

        /* ---------- main panel ---------- */
        var panelGO = new GameObject("Panel", typeof(RectTransform),
                                               typeof(Image));
        panelGO.transform.SetParent(transform, false);
        var panelRT = (RectTransform)panelGO.transform;
        panelRT.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelGO.GetComponent<Image>().color = COL_BG;

        /* ---------- scroll view ---------- */
        var scrollGO = new GameObject("Scroll",
                                      typeof(RectTransform),
                                      typeof(Image),
                                      typeof(ScrollRect),
                                      typeof(Mask));
        scrollGO.transform.SetParent(panelGO.transform, false);
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(16f, 16f);
        scrollRT.offsetMax = new Vector2(-16f, -16f);

        var mask = scrollGO.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        var scrollImg = scrollGO.GetComponent<Image>();
        scrollImg.color = Color.clear;

        /* ---------- viewport ---------- */
        var viewportRT = scrollRT; // scroll root doubles as viewport

        /* ---------- content container ---------- */
        var contentGO = new GameObject("Content",
                                       typeof(RectTransform),
                                       typeof(VerticalLayoutGroup),
                                       typeof(ContentSizeFitter));
        content = contentGO.transform;
        content.SetParent(viewportRT, false);

        var cRT = (RectTransform)content;
        cRT.anchorMin        = new Vector2(0f, 1f);
        cRT.anchorMax        = new Vector2(1f, 1f);
        cRT.pivot            = new Vector2(0.5f, 1f);
        cRT.anchoredPosition = Vector2.zero;

        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.spacing                = 4f;
        vlg.padding                = new RectOffset(0,0,2,2);

        var fitter = contentGO.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        /* ---------- ScrollRect wiring ---------- */
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.viewport = viewportRT;
        scroll.content  = (RectTransform)content;
        scroll.horizontal = false;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.scrollSensitivity = 20f;

        /* ---------- vertical scrollbar ---------- */
        var sbGO = new GameObject("Scrollbar",
                                  typeof(RectTransform),
                                  typeof(Image),
                                  typeof(Scrollbar));
        sbGO.transform.SetParent(panelGO.transform, false);
        var sbRT = (RectTransform)sbGO.transform;
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot     = new Vector2(1f, 1f);
        sbRT.sizeDelta = new Vector2(12f, -32f);
        sbRT.anchoredPosition = new Vector2(-4f, -16f);
        var sbImg = sbGO.GetComponent<Image>();
        sbImg.color = new Color(0.2f, 0.2f, 0.2f);
        var scrollbar = sbGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.2f;
        scrollbar.handleRect = new GameObject("Handle", typeof(RectTransform),
                                                       typeof(Image)).GetComponent<RectTransform>();
        scrollbar.handleRect.SetParent(sbRT, false);
        scrollbar.handleRect.anchorMin = Vector2.zero;
        scrollbar.handleRect.anchorMax = Vector2.one;
        scrollbar.handleRect.offsetMin = scrollbar.handleRect.offsetMax = Vector2.zero;
        scrollbar.handleRect.GetComponent<Image>().color = new Color(0.75f,0.75f,0.75f);
        scroll.verticalScrollbar = scrollbar;

        Refresh();
    }

    /* --------------------------------------------------------------------- */
    public void Toggle() { if (cg.alpha > 0.5f) Hide(); else Show(); }

    void Show()
    {
        Refresh();
        cg.alpha = 1f; cg.blocksRaycasts = cg.interactable = true;
        Time.timeScale = 0f;
    }
    void Hide()
    {
        cg.alpha = 0f; cg.blocksRaycasts = cg.interactable = false;
        Time.timeScale = 1f;
    }

    /* --------------------------------------------------------------------- */
    void Refresh()
    {
        foreach (Transform c in content) Destroy(c.gameObject);

        foreach (var r in crafting.AllRecipes)
        {
            bool canCraft = crafting.CanCraft(inv, r);
            Color baseCol = canCraft ? COL_CARD : COL_CARD_BAD;

            /* ---------- card ---------- */
            var card = new GameObject(r.name,
                                      typeof(RectTransform),
                                      typeof(Image),
                                      typeof(HorizontalLayoutGroup),
                                      typeof(LayoutElement),
                                      typeof(CardHoverTint));
            card.transform.SetParent(content, false);

            var le = card.GetComponent<LayoutElement>();
            le.preferredHeight = CARD_HEIGHT;
            le.flexibleWidth   = 1;

            var cardImg = card.GetComponent<Image>();
            cardImg.color = baseCol;

            var hover = card.GetComponent<CardHoverTint>();
            hover.Init(cardImg, baseCol, COL_CARD_HL);

            var hlg = card.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding        = new RectOffset(8, 8, 4, 4);
            hlg.spacing        = 8f;

            /* Icon */
            if (r.resultItem.icon)
            {
                var icon = new GameObject("Icon",
                                          typeof(RectTransform),
                                          typeof(Image),
                                          typeof(LayoutElement));
                icon.transform.SetParent(card.transform, false);
                ((RectTransform)icon.transform).sizeDelta = new Vector2(32,32);
                var ii = icon.GetComponent<Image>();
                ii.sprite = r.resultItem.icon;
                icon.GetComponent<LayoutElement>().preferredWidth = 32;
            }

            /* Label */
            var label = new GameObject("Label",
                                       typeof(RectTransform),
                                       typeof(TextMeshProUGUI),
                                       typeof(LayoutElement));
            label.transform.SetParent(card.transform, false);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.fontSize  = 17;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.color     = canCraft ? Color.white : new Color(0.7f,0.7f,0.7f);
            label.GetComponent<LayoutElement>().flexibleWidth = 1;

            var sb = new StringBuilder();
            sb.Append($"{r.resultAmount}× <b>{r.resultItem.itemName}</b>\n");
            sb.Append("<size=13><color=#CCCCCC>");
            foreach (var ing in r.ingredients)
            {
                int have = inv.CountItem(ing.item);
                sb.Append($"{have}/{ing.amount} {ing.item.itemName}   ");
            }
            sb.Append("</color></size>");
            tmp.text = sb.ToString();

            /* Craft button */
            var btn = new GameObject("Btn",
                                     typeof(RectTransform),
                                     typeof(Button),
                                     typeof(Image),
                                     typeof(LayoutElement),
                                     typeof(CardHoverTint));
            btn.transform.SetParent(card.transform, false);

            var btnLE = btn.GetComponent<LayoutElement>();
            btnLE.preferredWidth = 96;
            btnLE.preferredHeight = 36;

            var btnImg = btn.GetComponent<Image>();
            btnImg.color = canCraft ? new Color(0.22f,0.55f,0.22f,1f)
                                    : new Color(0.18f,0.18f,0.18f,1f);
            btn.GetComponent<CardHoverTint>()
                .Init(btnImg,
                      btnImg.color,
                      canCraft ? new Color(0.30f,0.70f,0.30f,1f)
                               : btnImg.color);

            /* text */
            var tGO = new GameObject("Txt",
                                     typeof(RectTransform),
                                     typeof(TextMeshProUGUI));
            tGO.transform.SetParent(btn.transform, false);
            var tRT = (RectTransform)tGO.transform;
            tRT.anchorMin = tRT.anchorMax = new Vector2(0.5f,0.5f);
            tRT.sizeDelta = Vector2.zero;

            var txt = tGO.GetComponent<TextMeshProUGUI>();
            txt.text = "Craft";
            txt.fontSize = 15;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = canCraft ? Color.white : new Color(0.6f,0.6f,0.6f);

            var button = btn.GetComponent<Button>();
            button.interactable = canCraft;
            if (canCraft)
                button.onClick.AddListener(() =>
                {
                    if (crafting.Craft(inv, r))
                        Refresh();
                });
        }
    }

    /* ---------------------------------------------------------------------
     *  Small helper: fades image colour when pointer hovers
     * -------------------------------------------------------------------*/
    private sealed class CardHoverTint :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        Image  img;
        Color  baseCol, hlCol;

        public void Init(Image target, Color baseC, Color hlC)
        {
            img = target;
            baseCol = baseC;
            hlCol   = hlC;
        }
        public void OnPointerEnter(PointerEventData _)
        {
            if (img) img.color = hlCol;
        }
        public void OnPointerExit(PointerEventData _)
        {
            if (img) img.color = baseCol;
        }
    }
} 