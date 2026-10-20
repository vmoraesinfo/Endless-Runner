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
        BuildStorePanel(canvas, ui, menus);
    }

    /// <summary>Botão "LOJA" no topo e painel com as compras reais (Remover anúncios e Pacote inicial).</summary>
    static void BuildStorePanel(Transform canvas, RectTransform ui, GarageMenus menus)
    {
        var old = canvas.Find("ShopPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var top = new Vector2(0.5f, 1);
        var center = new Vector2(0.5f, 0.5f);

        Button(ui, "ShopButton", "LOJA", Gold, new Vector2(0, 1), new Vector2(670, -75), new Vector2(130, 100), 32, menus.OpenShop)
            .GetComponentInChildren<TMP_Text>().color = new Color(0.3f, 0.18f, 0f);

        var panel = Stretch(NewRect("ShopPanel"), canvas);
        panel.gameObject.AddComponent<Image>().color = Dim;
        var card = Panel(panel, "Card", Card, center, center, Vector2.zero, new Vector2(980, 1260));
        Text(card, "Title", "LOJA", 84, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -100), new Vector2(900, 120));

        menus.noAdsButton = Offer(card, "NoAds", -320, "SEM ANÚNCIOS",
            "Tira o banner e os anúncios entre partidas.\nVídeos com prêmio continuam opcionais.", Purple, menus.BuyNoAds, out menus.noAdsPrice);
        Offer(card, "Starter", -660, "PACOTE INICIAL",
            $"+{Purchases.StarterCoins:N0} moedas para comprar\ncarros e melhorias agora.", Orange, menus.BuyStarter, out menus.starterPrice);

        menus.shopStatus = Text(card, "Status", "", 32, TextAlignmentOptions.Center, Gold, top, top, new Vector2(0, -880), new Vector2(900, 60));
        Button(card, "Restore", "RESTAURAR COMPRAS", Gray, new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(520, 100), 34, menus.RestorePurchases);
        Button(card, "Close", "FECHAR", Blue, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(500, 130), 52, menus.CloseShop);
        menus.shopPanel = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    static Button Offer(RectTransform card, string name, float y, string title, string desc, Color color, UnityAction buy, out TMP_Text price)
    {
        var top = new Vector2(0.5f, 1);
        var row = Panel(card, name, new Color(1f, 1f, 1f, 0.07f), top, top, new Vector2(0, y), new Vector2(900, 300));
        Text(row, "Title", title, 52, TextAlignmentOptions.Left, color == Gold ? Gold : Color.white,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(560, 70), new Vector2(0, 1));
        Text(row, "Description", desc, 32, TextAlignmentOptions.Left, new Color(0.8f, 0.85f, 0.95f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -115), new Vector2(560, 140), new Vector2(0, 1));
        var button = Button(row, "Buy", "R$ 0,00", color, new Vector2(1, 0.5f), new Vector2(-160, 0), new Vector2(270, 150), 40, buy);
        price = button.GetComponentInChildren<TMP_Text>();
        return button;
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
