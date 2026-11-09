using HarmonyLib;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Services;
using Il2CppReloaded.TreeStateActivities;
using Il2CppTekly.DataModels.Binders;
using Il2CppTekly.DataModels.Binders.Collections;
using Il2CppTekly.DataModels.Models;
using Il2CppTekly.PanelViews;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DifficultyMod;

internal sealed class LevelCard
{
    internal BinderContainer Container = null!;
    internal LevelEntryModel Model = null!;
    internal Image Trophy = null!;
}

internal sealed class NativeUi : IDisposable
{
    private readonly Mod mod;
    private readonly Dictionary<IntPtr, LevelCard> cards = new();
    private readonly Dictionary<IntPtr, LevelCard> actions = new();
    private readonly List<GameObject> settingsButtons = new();
    private GameObject? panelTemplate;
    private TrophyArt? trophy;
    private bool artFailed;
    private bool failed;
    private GameObject? window;
    private GameObject? previousSelection;
    private LevelCard? pendingCard;
    private string? pendingProfile;
    private bool settingsMode;
    private bool opening;
    private bool german;
    private ISettingsService? settings;
    private TMP_Text? titleLabel;
    private readonly List<TMP_Text> cancelLabels = new();
    private readonly List<(TMP_Text Label, Difficulty Difficulty)> optionLabels = new();
    internal bool Modal => opening || window != null;
    internal IUserService? UserService;

    internal NativeUi(Mod mod) => this.mod = mod;
    internal static Color Tint(int difficulty) => difficulty switch
    {
        1 => new(.20f, .53f, 1f, 1),
        2 => new(.88f, .92f, 1f, 1),
        3 => new(1f, .43f, .06f, 1),
        4 => new(1f, .82f, .16f, 1),
        _ => new(.10f, .11f, .14f, 1)
    };

    internal void Update()
    {
        // Trophy rendering must not depend on loading the selection window.
        if (trophy == null && !artFailed)
        {
            try { trophy = new TrophyArt(); }
            catch (Exception exception)
            {
                artFailed = true;
                mod.LoggerInstance.Error($"Native trophy artwork: {exception.Message}");
            }
        }
        if (opening && Ready)
        {
            opening = false;
            if (!settingsMode && (pendingProfile != mod.Profile || pendingCard == null ||
                !pendingCard.Container || !pendingCard.Container.gameObject.activeInHierarchy || pendingCard.Model.Locked))
            { Close(); return; }
            try { BuildWindow(); }
            catch (Exception exception) { Fail($"Native difficulty window: {exception}"); }
        }
        if (Modal && KeyboardEscape()) Close();
        if (window != null && !settingsMode && pendingProfile != mod.Profile) Close();
    }

    private bool Ready => !failed && panelTemplate != null && panelTemplate && trophy != null;

    private void FindPanelTemplate()
    {
        if (panelTemplate != null && panelTemplate) return;
        // The frontend already owns the native panel, including inactive views.
        // The prefab's filename is an internal catalog ID, not a loadable key.
        foreach (var panel in Resources.FindObjectsOfTypeAll<PanelView>())
        {
            if (!panel || panel.Id != "minigames") continue;
            if (panel.GetComponentInChildren<Canvas>(true) == null) continue;
            panelTemplate = panel.gameObject;
            mod.LoggerInstance.Msg("Using loaded native minigames panel for difficulty selection.");
            return;
        }
    }
    private static bool KeyboardEscape() => UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
        UnityEngine.InputSystem.Gamepad.current?.buttonEast.wasPressedThisFrame == true;
    private void Fail(string message)
    {
        failed = true; Close(); mod.LoggerInstance.Error(message);
    }

    internal void Scan()
    {
        RefreshLanguage();
        FindPanelTemplate();
        foreach (var pointer in cards.Where(item => !item.Value.Container).Select(item => item.Key).ToArray()) cards.Remove(pointer);
        actions.Clear();
        foreach (var container in Resources.FindObjectsOfTypeAll<BinderContainer>())
        {
            if (!container || !container.gameObject.activeInHierarchy ||
                window != null && container.transform.IsChildOf(window.transform)) continue;
            string name = container.gameObject.name;
            if (!name.StartsWith("P_LevelSelect_ListItem") && !name.StartsWith("P_LevelPanel_LevelItem") &&
                !name.StartsWith("P_ChallengePanel_ChallengeItem")) continue;
            try
            {
                if (!container.m_hasBound) continue;
                string keyPath = container.ResolveFullKey("*");
                if (string.IsNullOrEmpty(keyPath) || !container.TryGet(keyPath, out IModel raw)) continue;
                var model = raw.TryCast<LevelEntryModel>();
                if (model?.EntryData == null) continue;
                UserService = model.m_userService;
                if (!cards.TryGetValue(container.Pointer, out var card))
                {
                    var image = NewImage(container.transform, "DifficultyTrophy");
                    var rect = image.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(20, 20);
                    float size = name.StartsWith("P_LevelSelect") ? 100 : 64;
                    rect.sizeDelta = new Vector2(size, size);
                    image.raycastTarget = false;
                    image.preserveAspect = true;
                    cards[container.Pointer] = card = new() { Container = container, Trophy = image };
                    mod.LoggerInstance.Msg($"Bound difficulty card: {model.EntryData.name} ({keyPath}).");
                }
                card.Model = model;
                int best = mod.Key(model.EntryData) is { } key ? mod.Progress.Best(key) : 0;
                card.Trophy.enabled = best > 0 && trophy != null;
                if (trophy != null) card.Trophy.sprite = trophy.Sprite;
                card.Trophy.color = Tint(best);
                card.Trophy.transform.SetAsLastSibling();
                if (model.m_selectButtonModel != null) actions[model.m_selectButtonModel.Pointer] = card;
            }
            catch (Exception exception)
            {
                mod.LoggerInstance.Error($"Cannot bind difficulty card {name}: {exception.Message}");
                return;
            }
        }
        AddSettingsButton();
    }

    private void RefreshLanguage()
    {
        settings ??= mod.App?.SettingsService;
        if (settings == null)
            foreach (var activity in Resources.FindObjectsOfTypeAll<LocalizationActivity>())
                if (activity && activity.m_settings != null) { settings = activity.m_settings; break; }
        if (settings == null) return;
        // This native setting drives LocalizationActivity. OS/Steam language
        // and the current difficulty have no bearing on menu translation.
        german = settings.CurrentLanguage == Language.German;
        if (titleLabel != null && titleLabel) titleLabel.text = DifficultyText.Title(settingsMode, german);
        foreach (var (label, difficulty) in optionLabels)
            if (label) label.text = DifficultyText.Name(difficulty, german);
        foreach (var label in cancelLabels) if (label) label.text = DifficultyText.Cancel(german);
        foreach (var button in settingsButtons)
            if (button)
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                    label.text = DifficultyText.Settings(german);
    }

    internal LevelCard? Find(Component component)
    {
        for (var current = component.transform; current != null; current = current.parent)
        {
            if (!current.name.StartsWith("P_LevelSelect_ListItem") && !current.name.StartsWith("P_LevelPanel_LevelItem") &&
                !current.name.StartsWith("P_ChallengePanel_ChallengeItem")) continue;
            if (current.GetComponent<BinderContainer>() is not { } container) continue;
            // Bindings can change when a native list recycles a card. Resolve
            // again at input time, including clicks before the periodic scan.
            Scan();
            if (cards.TryGetValue(container.Pointer, out var card)) return card;
        }
        return null;
    }

    internal bool Activate(ButtonModel action)
    {
        if (mod.Launching) return true;
        if (Modal) return false;
        if (actions.TryGetValue(action.Pointer, out var card) && !card.Model.Locked)
            mod.Select(card.Model, DifficultyRules.Validate(mod.Default.Value));
        return true;
    }

    internal void Open(LevelCard? card, bool settings = false)
    {
        if (Modal || failed || !settings && (card == null || card.Model.Locked)) return;
        FindPanelTemplate();
        if (!Ready)
        {
            mod.LoggerInstance.Warning("Native difficulty panel is not available yet; selection was not started.");
            return;
        }
        pendingCard = card; pendingProfile = mod.Profile; settingsMode = settings;
        previousSelection = EventSystem.current?.currentSelectedGameObject;
        opening = true;
    }

    private void BuildWindow()
    {
        // An inactive parent prevents cloned binders, transitions and native
        // minigame actions from running even once. Only native UI visuals remain.
        window = new GameObject("DifficultyModWindow");
        window.SetActive(false);
        var panel = Object.Instantiate(panelTemplate!, window.transform);
        var contentPanel = Descendant(panel.transform, "Panel")!;
        var gridTransform = Descendant(contentPanel, "GridLayout")!;
        // Native lists retain their serialized template even after creating and
        // naming live entries. Copy that template before removing list binders.
        var list = gridTransform.GetComponent<ListBinder>()
            ?? throw new InvalidDataException("Native minigame list binder is missing.");
        var templateSource = list.m_template
            ?? throw new InvalidDataException("Native minigame card template is missing.");
        var template = Object.Instantiate(templateSource.gameObject, window.transform);
        template.SetActive(false);
        Sanitize(template);
        Sanitize(panel);
        var canvas = panel.GetComponentInChildren<Canvas>(true);
        // A loaded native view can be hidden. Activate only our sanitized copy.
        panel.SetActive(true);
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        var blocker = NewImage(canvas.transform, "DifficultyModalBlocker");
        Stretch(blocker.rectTransform);
        blocker.color = new Color(0, 0, 0, .45f);
        blocker.raycastTarget = true;
        blocker.transform.SetAsFirstSibling();
        titleLabel = Descendant(contentPanel, "HeaderText")?.GetComponent<TMP_Text>();
        if (titleLabel != null)
        {
            titleLabel.text = DifficultyText.Title(settingsMode, german);
            FitLabel(titleLabel, 90);
        }
        var count = Descendant(contentPanel, "TrophyCounter");
        if (count != null) count.gameObject.SetActive(false);
        var legend = Descendant(panel.transform, "P_ControllerPrompt_Legend");
        if (legend != null) legend.gameObject.SetActive(false);
        for (int i = gridTransform.childCount - 1; i >= 0; i--)
        {
            var child = gridTransform.GetChild(i);
            child.gameObject.SetActive(false);
            Object.Destroy(child.gameObject);
        }
        var grid = gridTransform.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(250, 320);
        grid.spacing = new Vector2(20, 20);
        grid.childAlignment = TextAnchor.MiddleCenter;
        var scroll = gridTransform.GetComponentInParent<ScrollRect>();
        if (scroll != null) { scroll.horizontal = false; scroll.vertical = false; }
        Button? first = null;
        for (int i = 1; i <= 5; i++)
        {
            Difficulty difficulty = (Difficulty)i;
            var option = Object.Instantiate(template, gridTransform);
            option.name = "DifficultyOption" + i;
            foreach (var transform in option.GetComponentsInChildren<Transform>(true))
            {
                string name = transform.name;
                if (name is "ChallengeThumbnail_Locked" or "ChallengeThumbnail_UnLocked" or "Background_Locked" or
                    "TrophyFlag" or "StreakText" or "VisiblityBinders" || name.Trim() == "ChallengeDisabledNameText")
                    transform.gameObject.SetActive(false);
                if (name == "ChallengeThumbnail")
                {
                    transform.gameObject.SetActive(true);
                    var image = transform.GetComponent<Image>();
                    image.sprite = trophy!.Sprite; image.color = Tint(i); image.preserveAspect = true;
                    // Reserve disjoint areas inside the native frame for the
                    // trophy and caption instead of inheriting 426px card offsets.
                    var rect = image.rectTransform;
                    rect.anchorMin = new Vector2(0, .40f);
                    rect.anchorMax = new Vector2(1, .84f);
                    rect.offsetMin = new Vector2(44, 0);
                    rect.offsetMax = new Vector2(-44, 0);
                }
                if (name is "Show_Challenge" or "BackgroundBlank") transform.gameObject.SetActive(true);
                if (name == "ChallengeNameText")
                {
                    transform.gameObject.SetActive(true);
                    var label = transform.GetComponent<TMP_Text>();
                    label.text = DifficultyText.Name(difficulty, german);
                    var rect = label.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(.5f, 0);
                    rect.anchoredPosition = new Vector2(0, 62);
                    rect.sizeDelta = new Vector2(-80, 64);
                    FitLabel(label, 42);
                    optionLabels.Add((label, difficulty));
                }
            }
            var button = option.GetComponent<Button>();
            button.interactable = true;
            button.targetGraphic = Descendant(option.transform, "BackgroundBlank")?.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.82f, 1, .72f);
            colors.selectedColor = new Color(.82f, 1, .72f);
            button.colors = colors;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() => Choose(difficulty)));
            if (i == (settingsMode ? mod.Default.Value : 3)) first = button;
            option.SetActive(true);
        }
        var closeTransform = Descendant(contentPanel, "P_PanelButton")!;
        var closeButton = closeTransform.GetComponent<Button>() ?? closeTransform.GetComponentInChildren<Button>(true);
        closeButton.onClick = new Button.ButtonClickedEvent();
        closeButton.onClick.AddListener((UnityEngine.Events.UnityAction)Close);
        foreach (var label in closeTransform.GetComponentsInChildren<TMP_Text>(true))
        {
            label.text = DifficultyText.Cancel(german);
            FitLabel(label, 48);
            cancelLabels.Add(label);
        }
        window.SetActive(true);
        first?.Select();
    }

    private void Choose(Difficulty difficulty)
    {
        if (settingsMode)
        {
            mod.Default.Value = (int)difficulty; mod.Settings.SaveToFile(); Close(); return;
        }
        var card = pendingCard;
        if (card == null || !card.Container || card.Model.Locked || pendingProfile != mod.Profile) { Close(); return; }
        mod.Select(card.Model, difficulty);
        Close();
        mod.Launching = true;
        try { card.Model.m_selectButtonModel.Activate(0); }
        finally { mod.Launching = false; }
    }

    private void AddSettingsButton()
    {
        if (!Ready) return;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            var root = canvas.transform;
            if (!root || !root.gameObject.activeInHierarchy || !root.name.StartsWith("P_OptionsPanel_Canvas")) continue;
            var rect = Descendant(root, "Panel")?.TryCast<RectTransform>();
            if (rect == null || rect.Find("DifficultyModSettings") != null) continue;
            var source = Descendant(panelTemplate!.transform, "P_PanelButton");
            if (source == null) continue;
            var staging = new GameObject("DifficultySettingsStaging");
            staging.SetActive(false);
            var buttonObject = Object.Instantiate(source.gameObject, staging.transform);
            Sanitize(buttonObject);
            buttonObject.name = "DifficultyModSettings";
            buttonObject.transform.SetParent(rect, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1, 1);
            buttonRect.pivot = new Vector2(1, 1);
            buttonRect.anchoredPosition = new Vector2(-25, -25);
            buttonRect.sizeDelta = new Vector2(460, 80);
            var layout = buttonObject.GetComponent<LayoutElement>() ?? buttonObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            foreach (var text in buttonObject.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = DifficultyText.Settings(german);
                FitLabel(text, 48);
            }
            var button = buttonObject.GetComponent<Button>();
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() => Open(null, true)));
            buttonObject.SetActive(true);
            settingsButtons.Add(buttonObject);
            Object.Destroy(staging);
        }
    }

    private static void Sanitize(GameObject root)
    {
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            // Bindings and gameplay scripts must never run in a menu clone.
            bool keep = behaviour.TryCast<Graphic>() != null || behaviour.TryCast<Selectable>() != null ||
                behaviour.TryCast<LayoutGroup>() != null || behaviour.TryCast<LayoutElement>() != null ||
                behaviour.TryCast<ContentSizeFitter>() != null || behaviour.TryCast<ScrollRect>() != null ||
                behaviour.TryCast<Mask>() != null || behaviour.TryCast<RectMask2D>() != null ||
                behaviour.TryCast<CanvasScaler>() != null || behaviour.TryCast<GraphicRaycaster>() != null;
            if (!keep)
            {
                behaviour.enabled = false;
                Object.DestroyImmediate(behaviour);
            }
        }
        foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
        foreach (var toggle in root.GetComponentsInChildren<Toggle>(true)) toggle.enabled = false;
    }

    private static Image NewImage(Transform parent, string name)
    {
        var gameObject = new GameObject(name);
        gameObject.layer = parent.gameObject.layer;
        gameObject.AddComponent<RectTransform>();
        gameObject.transform.SetParent(parent, false);
        return gameObject.AddComponent<Image>();
    }
    private static void FitLabel(TMP_Text label, float maximumSize)
    {
        label.enableAutoSizing = true;
        label.fontSizeMin = 20;
        label.fontSizeMax = maximumSize;
        label.fontSize = maximumSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.margin = new Vector4(4, 4, 4, 4);
        label.lineSpacing = 0;
        label.raycastTarget = false;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static Transform? Descendant(Transform parent, string name)
    {
        foreach (var child in parent.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
        return null;
    }
    internal void Close()
    {
        opening = false;
        titleLabel = null;
        optionLabels.Clear(); cancelLabels.Clear();
        if (window != null) { window.SetActive(false); Object.Destroy(window); window = null; }
        if (previousSelection != null && previousSelection && previousSelection.activeInHierarchy)
            EventSystem.current?.SetSelectedGameObject(previousSelection);
        previousSelection = null; pendingCard = null; pendingProfile = null;
    }
    public void Dispose()
    {
        Close();
        foreach (var card in cards.Values) if (card.Trophy) Object.Destroy(card.Trophy.gameObject);
        cards.Clear(); actions.Clear();
        foreach (var button in settingsButtons) if (button) Object.Destroy(button);
        settingsButtons.Clear();
        panelTemplate = null; // Borrowed from the frontend; never destroy it.
        trophy?.Dispose(); trophy = null;
    }
}

[HarmonyPatch(typeof(Selectable), nameof(Selectable.OnPointerDown))]
internal static class CardPointerDownPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Selectable __instance, PointerEventData eventData)
    {
        var ui = Mod.Instance.Ui;
        var card = ui.Find(__instance);
        if (card == null) return true;
        if (ui.Modal) return false;
        if (eventData.button != PointerEventData.InputButton.Right) return true;
        if (__instance.IsInteractable() && !card.Model.Locked) ui.Open(card);
        return false;
    }
}

[HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
internal static class CardPointerClickPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Button __instance, PointerEventData eventData)
    {
        var ui = Mod.Instance.Ui;
        if (ui.Find(__instance) == null) return true;
        return !ui.Modal && eventData.button != PointerEventData.InputButton.Right;
    }
}

[HarmonyPatch(typeof(ButtonModel), nameof(ButtonModel.Activate))]
internal static class CardActivatePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ButtonModel __instance) => Mod.Instance.Ui.Activate(__instance);
}
