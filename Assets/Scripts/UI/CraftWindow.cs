using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Крафт [K] по рецептам старой игры (recipes.json). Рецепт показывает, чего не хватает:
// материалов, инструментов (не тратятся), огня рядом или верстака. Изготовление занимает
// игровое время (минуты из рецепта) и даёт опыт навыка.
// Рецепты с place ставят объект в мир: костёр, верстак, буржуйку, сигнализацию из банок.
public class CraftWindow : GameWindows.Window
{
    RectTransform list;

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Крафт", new Vector2(1200f, 820f), out root, GameWindows.Close);
        list = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(1140f, 700f), 10f);
    }

    public override void Refresh()
    {
        UIFactory.Clear(list);
        var player = GameObject.FindWithTag("Player");
        Vector3 at = player != null ? player.transform.position : Vector3.zero;

        foreach (var recipe in GameData.Recipes.Values)
        {
            if (!GameSession.KnowsRecipe(recipe.id))
                continue;
            var missing = Missing(recipe, at);
            var parts = new List<string>();
            foreach (var pair in recipe.inputs)
                parts.Add(Colored($"{Items.Name(pair.Key)} {GameSession.Count(pair.Key)}/{pair.Value}", GameSession.Count(pair.Key) >= pair.Value));
            foreach (var tool in recipe.tools)
                parts.Add(Colored($"[{Items.Name(tool)}]", GameSession.Has(tool)));
            if (recipe.station == "fire") parts.Add(Colored("у огня", Campfire.Near(at, 3.5f)));
            if (recipe.station == "workbench") parts.Add(Colored("у верстака", Workbench.Near(at, 3f)));

            UIFactory.Line(list, $"<b>{recipe.name}</b>   {string.Join(", ", parts)}");
            var r = recipe;
            var button = UIFactory.SmallButton(list, missing == null ? "Сделать" : missing, () => Craft(r), 360f);
            button.interactable = missing == null;
        }
        UIFactory.Line(list, "\nНовые рецепты дают книги и жители «Зари».", 18);
    }

    static string Colored(string text, bool ok) => ok ? text : $"<color=#e07050>{text}</color>";

    // Чего не хватает для рецепта (текст для кнопки) или null, если всё есть.
    static string Missing(GameData.RecipeDef recipe, Vector3 at)
    {
        foreach (var pair in recipe.inputs)
            if (GameSession.Count(pair.Key) < pair.Value)
                return "Не хватает материалов";
        foreach (var tool in recipe.tools)
            if (!GameSession.Has(tool))
                return $"Нужен инструмент: {Items.Name(tool)}";
        if (recipe.station == "fire" && !Campfire.Near(at, 3.5f))
            return "Нужен огонь рядом";
        if (recipe.station == "workbench" && !Workbench.Near(at, 3f))
            return "Нужен верстак рядом";
        return null;
    }

    void Craft(GameData.RecipeDef recipe)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null || Missing(recipe, player.transform.position) != null)
            return;
        foreach (var pair in recipe.inputs)
            GameSession.Remove(pair.Key, pair.Value);
        foreach (var pair in recipe.outputs)
            GameSession.Add(pair.Key, pair.Value);
        if (!string.IsNullOrEmpty(recipe.place))
            Placeables.Place(recipe.place, player.transform.position + player.transform.forward * 1.5f);

        // Работа занимает игровое время, как в старой игре.
        GameSession.Data.minutes += recipe.time;
        GameSession.AddXp(recipe.skill, 10f + recipe.time * 2f);
        Sfx.Play(SoundId.Crate, player.transform.position, 0.3f);
        GameMessages.Show($"Сделано: {recipe.name}");
    }
}
