using UnityEngine;
using System.Collections.Generic;

public class SelectCars : MonoBehaviour
{
    private List<GameObject> Cars;
    public GameObject carHolder;
    private int carIndex = 0;

    void Start()
    {
        Cars = GetAllChildren(carHolder);
        DisplayCars();
    }

    public void NextCar()
    {
        if (carIndex < Cars.Count - 1)
        {
            carIndex++;
        } else {
            carIndex =0;
        }
        DisplayCars();

    }

    public void PreviousCar()
    {
        if (carIndex > 0)
        {
            carIndex--;
        } else {
            carIndex = Cars.Count - 1;
        }
        DisplayCars();

    }

    void DisplayCars()
    {
        for (int i = 0; i < Cars.Count; i++)
        {
            Cars[i].SetActive(i == carIndex);
        }
    }

      List<GameObject> GetAllChildren(GameObject parent)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (Transform child in parent.transform)
        {
            child.gameObject.AddComponent(System.Type.GetType("Rotator"));

            result.Add(child.gameObject);
        }

        return result;
    }
}
