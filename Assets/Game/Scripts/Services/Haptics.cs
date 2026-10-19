using UnityEngine;

public static class Haptics
{
    public static void Crash()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (SaveData.Vibration) Handheld.Vibrate();
#endif
    }
}
