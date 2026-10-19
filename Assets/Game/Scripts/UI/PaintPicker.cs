using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fileira de bolinhas de cor na garagem. Toque numa cor comprada para pintar o carro;
/// toque numa cor bloqueada para comprar (Paints.Price moedas). Só aparece para carros que o jogador já tem.
/// </summary>
public class PaintPicker : MonoBehaviour
{
    public SelectCars garage;
    public GameObject root;
    public Image[] swatches;
    public GameObject[] locks;
    public GameObject[] selectedRings;
    public TMP_Text label;

    void OnEnable()
    {
        if (garage != null) garage.CarChanged += Refresh;
    }

    void OnDisable()
    {
        if (garage != null) garage.CarChanged -= Refresh;
    }

    void Start() => Refresh();

    // Botões de UI aceitam no máximo 1 parâmetro; um método por cor deixa a ligação explícita.
    public void Pick0() => Pick(0);
    public void Pick1() => Pick(1);
    public void Pick2() => Pick(2);
    public void Pick3() => Pick(3);
    public void Pick4() => Pick(4);
    public void Pick5() => Pick(5);

    public void Pick(int paint)
    {
        int car = garage.CurrentCar;
        if (!garage.CurrentOwned) return;
        if (Paints.IsOwned(car, paint)) Paints.Select(car, paint);
        else if (!Paints.TryBuy(car, paint)) { label.text = $"FALTAM MOEDAS ({Paints.Price})"; return; }
        Sfx.I.Click();
        CarPaint.Apply(garage.CurrentCarObject, Paints.Selected(car));
        garage.RefreshUI();
        Refresh();
    }

    public void Refresh()
    {
        bool show = garage != null && garage.CurrentOwned;
        root.SetActive(show);
        if (!show) return;

        int car = garage.CurrentCar;
        int selected = Paints.Selected(car);
        for (int i = 0; i < swatches.Length && i < Paints.All.Length; i++)
        {
            locks[i].SetActive(!Paints.IsOwned(car, i));
            selectedRings[i].SetActive(i == selected);
        }
        label.text = $"PINTURA: {Paints.All[selected].name}   |   NOVA COR: {Paints.Price}";
    }
}
