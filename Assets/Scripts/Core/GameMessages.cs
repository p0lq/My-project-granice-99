using System;

// Короткие сообщения в углу экрана: «Подобрал: батарейки», «Заперто».
// Любой скрипт пишет сюда, HUD показывает. Так предметам и дверям не нужна ссылка на интерфейс.
public static class GameMessages
{
    public static event Action<string> Posted;

    public static void Show(string text) => Posted?.Invoke(text);

    // Подписчики из прошлого запуска Play (перезагрузка домена выключена, см. GameSession).
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Posted = null;
}
