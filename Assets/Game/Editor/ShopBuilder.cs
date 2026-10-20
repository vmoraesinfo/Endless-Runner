using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Terceira parte do GameBuilder: loja da garagem — painel de melhorias e fileira de pinturas.
/// </summary>
public static partial class GameBuilder
{
    static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    static void BuildShopUI(Transform canvas, RectTransform ui, SelectCars select)
    {
        var old = canvas.Find("UpgradesPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var menus = ui.GetComponent<GarageMenus>();
        var top = new Vector2(0.5f, 1);
        var center = new Vector2(0.5f, 0.5f);

        // Botão "MELHORIAS" no canto direito, espelhando o de missões.
        Button(ui, "UpgradesButton", "MELHORIAS", Purple, new Vector2(0, 1), new Vector2(450, -75), new Vector2(280, 100), 38, menus.OpenUpgrades);

        // Painel de melhorias
        var panel = Stretch(NewRect("UpgradesPanel"), canvas);
        panel.gameObject.AddComponent<Image>().color = Dim;
        var card = Panel(panel, "Card", Card, center, center, Vector2.zero, new Vector2(980, 1300));
        Text(card, "Title", "MELHORIAS", 84, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -100), new Vector2(900, 120));
        var types = new[] { UpgradeType.Nitro, UpgradeType.Magnet, UpgradeType.Shield };
        var colors = new[] { new Color(0.35f, 0.8f, 1f), new Color(1f, 0.45f, 0.4f), new Color(0.5f, 0.75f, 1f) };
        UnityAction[] buys = { menus.BuyNitro, menus.BuyMagnet, menus.BuyShield };
        menus.upgradeRows = new GarageMenus.UpgradeRow[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            var row = Panel(card, types[i] + "Row", new Color(1f, 1f, 1f, 0.06f), top, top, new Vector2(0, -290 - i * 300), new Vector2(900, 260));
            Text(row, "Title", Upgrades.Title(types[i]), 50, TextAlignmentOptions.Left, colors[i],
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -25), new Vector2(520, 70), new Vector2(0, 1));
            var r = new GarageMenus.UpgradeRow { type = types[i] };
            r.description = Text(row, "Description", "", 32, TextAlignmentOptions.Left, new Color(0.8f, 0.85f, 0.95f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -100), new Vector2(560, 60), new Vector2(0, 1));
            r.pips = new Image[Upgrades.MaxLevel];
            for (int p = 0; p < Upgrades.MaxLevel; p++)
            {
                var pip = Place(NewRect("Pip" + p), row, new Vector2(0, 0), new Vector2(0, 0), new Vector2(70 + p * 72, 50), new Vector2(60, 26));
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = roundSprite;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                r.pips[p] = img;
            }
            r.buyButton = Button(row, "Buy", "MELHORAR", Green, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(290, 130), 36, buys[i]);
            r.buyLabel = r.buyButton.GetComponentInChildren<TMP_Text>();
            menus.upgradeRows[i] = r;
        }
        Button(card, "Close", "FECHAR", Blue, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(500, 130), 52, menus.CloseUpgrades);
        menus.upgradesPanel = panel.gameObject;
        // Fica acima dos outros painéis da garagem.
        panel.SetAsLastSibling();

        BuildPaintPicker(ui, select);
    }

    static void BuildPaintPicker(RectTransform ui, SelectCars select)
    {
        var picker = ui.gameObject.AddComponent<PaintPicker>();
        picker.garage = select;
        var top = new Vector2(0.5f, 1);
        var root = Place(NewRect("PaintPicker"), ui, top, top, new Vector2(0, -470), new Vector2(940, 190));
        picker.root = root.gameObject;

        int n = Paints.All.Length;
        picker.swatches = new Image[n];
        picker.locks = new GameObject[n];
        picker.selectedRings = new GameObject[n];
        UnityAction[] picks = { picker.Pick0, picker.Pick1, picker.Pick2, picker.Pick3, picker.Pick4, picker.Pick5 };
        const float spacing = 130f;
        for (int i = 0; i < n; i++)
        {
            float x = (i - (n - 1) * 0.5f) * spacing;
            var slot = Place(NewRect("Paint" + i), root, top, top, new Vector2(x, -55), new Vector2(110, 110));

            var ring = Place(NewRect("Selected"), slot, center: true);
            ring.sizeDelta = new Vector2(116, 116);
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = Knob;
            ringImg.color = Color.white;
            ringImg.raycastTarget = false;
            picker.selectedRings[i] = ring.gameObject;

            var sw = Place(NewRect("Swatch"), slot, center: true);
            sw.sizeDelta = new Vector2(92, 92);
            var swImg = sw.gameObject.AddComponent<Image>();
            swImg.sprite = Knob;
            swImg.color = i == 0 ? new Color(0.75f, 0.75f, 0.78f) : Paints.All[i].color;
            var button = sw.gameObject.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, picks[i]);
            picker.swatches[i] = swImg;
            if (i == 0) Text(sw, "Label", "ORIG", 24, TextAlignmentOptions.Center, new Color(0.2f, 0.2f, 0.25f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var lockGo = Place(NewRect("Lock"), slot, center: true);
            lockGo.sizeDelta = new Vector2(92, 92);
            var lockImg = lockGo.gameObject.AddComponent<Image>();
            lockImg.sprite = Knob;
            lockImg.color = new Color(0f, 0f, 0f, 0.55f);
            lockImg.raycastTarget = false;
            Text(lockGo, "Price", Paints.Price.ToString(), 28, TextAlignmentOptions.Center, Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            picker.locks[i] = lockGo.gameObject;
        }
        picker.label = Text(root, "Label", "PINTURA", 30, TextAlignmentOptions.Center, Color.white,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(920, 44));
    }

    /// <summary>Retângulo centralizado no pai (âncora e pivô no meio).</summary>
    static RectTransform Place(RectTransform rt, Transform parent, bool center)
    {
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }
}
