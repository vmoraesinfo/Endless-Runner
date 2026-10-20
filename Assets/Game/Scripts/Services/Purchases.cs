using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// Compras dentro do app (Unity IAP 5 → Google Play Billing).
///  - remove_ads (não consumível): tira banner e intersticiais. Vídeos com recompensa continuam opcionais.
///  - starter_pack (consumível): +StarterCoins moedas.
/// Os produtos precisam existir no Play Console (Monetizar → Produtos → Produtos no app) com estes IDs.
/// Cria-se sozinho na primeira chamada e restaura "remove_ads" ao reinstalar.
/// </summary>
public class Purchases : MonoBehaviour
{
    public const string RemoveAdsId = "remove_ads";
    public const string StarterPackId = "starter_pack";
    public const int StarterCoins = 3000;

    static Purchases instance;
    StoreController store;
    bool connected;
    Action<bool> pendingCallback;

    /// <summary>Preços vindos da loja (já no formato da moeda local), ou null enquanto não carregaram.</summary>
    public static event Action Changed;

    public static Purchases Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Purchases");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Purchases>();
            }
            return instance;
        }
    }

    public bool Ready => connected && store != null && store.GetProducts().Count > 0;

    async void Awake()
    {
        // No teste automatizado não há loja; no editor o IAP usa uma loja simulada.
        if (Application.isBatchMode) return;
        try
        {
            store = UnityIAPServices.StoreController();
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseFailed += OnPurchaseFailed;
            store.OnPurchasesFetched += OnPurchasesFetched;
            store.OnProductsFetched += _ => { store.FetchPurchases(); Changed?.Invoke(); };
            store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] Produtos não carregaram: " + f.FailureReason);
            store.OnStoreDisconnected += d => { connected = false; Debug.LogWarning("[IAP] Loja desconectada: " + d.message); };

            await store.Connect();
            connected = true;
            store.FetchProducts(new List<ProductDefinition>
            {
                new ProductDefinition(RemoveAdsId, ProductType.NonConsumable),
                new ProductDefinition(StarterPackId, ProductType.Consumable),
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Não foi possível conectar à loja: " + e.Message);
        }
    }

    /// <summary>Preço local ("R$ 9,90") ou o texto padrão enquanto a loja não respondeu.</summary>
    public string Price(string productId, string fallback)
    {
        var p = store?.GetProducts().FirstOrDefault(x => x.definition.id == productId);
        return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : fallback;
    }

    public void Buy(string productId, Action<bool> done)
    {
        var product = store?.GetProducts().FirstOrDefault(x => x.definition.id == productId);
        if (!Ready || product == null || !product.availableToPurchase)
        {
            Debug.LogWarning("[IAP] Produto indisponível: " + productId);
            done?.Invoke(false);
            return;
        }
        pendingCallback = done;
        store.PurchaseProduct(product);
    }

    public void Restore() => store?.FetchPurchases();

    void OnPurchasePending(PendingOrder order)
    {
        foreach (var item in order.CartOrdered.Items()) Grant(item.Product.definition.id);
        // Entrega primeiro, confirma depois (se o app fechar no meio, a loja reenvia o pedido pendente).
        store.ConfirmPurchase(order);
        pendingCallback?.Invoke(true);
        pendingCallback = null;
        Changed?.Invoke();
    }

    void OnPurchaseFailed(FailedOrder order)
    {
        Debug.LogWarning($"[IAP] Compra falhou: {order.FailureReason} {order.Details}");
        pendingCallback?.Invoke(false);
        pendingCallback = null;
    }

    void OnPurchasesFetched(Orders orders)
    {
        // Compras antigas: só "remove_ads" precisa ser restaurado. Pendentes são entregues e confirmadas.
        foreach (var order in orders.ConfirmedOrders)
            foreach (var item in order.CartOrdered.Items())
                if (item.Product.definition.id == RemoveAdsId) Grant(RemoveAdsId);
        foreach (var order in orders.PendingOrders) OnPurchasePending(order);
        Changed?.Invoke();
    }

    static void Grant(string productId)
    {
        switch (productId)
        {
            case RemoveAdsId:
                if (!SaveData.NoAds)
                {
                    SaveData.NoAds = true;
                    AdsManager.Instance.HideBanner();
                }
                break;
            case StarterPackId:
                SaveData.Coins += StarterCoins;
                break;
        }
    }
}
