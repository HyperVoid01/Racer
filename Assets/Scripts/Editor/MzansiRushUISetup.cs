using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor menu: Tools > Mzansi Rush > Create Editable Game UI.
/// Run it once with the GameLoop scene open. It creates the run summary texts, the score popup template and the
/// touch controls as normal objects in your UI canvas and assigns them to the UIController, so you can move,
/// resize and restyle them like the rest of your UI. Things that already exist are left alone, so it is safe to
/// run again (for example after deleting something you want recreated).
/// </summary>
public static class MzansiRushUISetup
{
    private const string SpriteFolder = "Assets/UI/TouchControls";

    [MenuItem("Tools/Mzansi Rush/Create Editable Game UI")]
    private static void CreateEditableGameUI()
    {
        var ui = Object.FindFirstObjectByType<UIController>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Mzansi Rush",
                "No UIController found. Open the GameLoop scene and try again.", "OK");
            return;
        }

        var created = new List<string>();
        var so = new SerializedObject(ui);
        var scoreText = so.FindProperty("scoreText").objectReferenceValue as TextMeshProUGUI;
        var deathScreen = so.FindProperty("deathScreen").objectReferenceValue as GameObject;

        CreateRunSummary(so, deathScreen, created);
        CreateScorePopupTemplate(so, scoreText, created);
        CreateTouchControls(so, ui, scoreText, created);

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        string message = created.Count == 0
            ? "Everything already exists and is assigned on the UIController. Nothing was changed."
            : "Created:\n- " + string.Join("\n- ", created) +
              "\n\nThey are assigned on the UIController. Move and restyle them like any other UI, then save the scene (Ctrl+S)." +
              "\n\nTouch controls only show on phones and in the Device Simulator. Tick 'Show In Editor' on the MobileControls object to see them in the Game view.";
        EditorUtility.DisplayDialog("Mzansi Rush", message, "OK");
    }

    // ---------------------------------------------------------------- Run summary

    private static void CreateRunSummary(SerializedObject so, GameObject deathScreen, List<string> created)
    {
        SerializedProperty reasonProp = so.FindProperty("gameOverReasonText");
        SerializedProperty fareProp = so.FindProperty("fareText");
        SerializedProperty bestProp = so.FindProperty("bestText");

        if (reasonProp.objectReferenceValue != null && fareProp.objectReferenceValue != null &&
            bestProp.objectReferenceValue != null) return;

        if (deathScreen == null)
        {
            Debug.LogWarning("[Mzansi Rush] UIController has no Death Screen assigned; skipped the run summary.");
            return;
        }

        // The "GAME OVER" title: the first text on the death screen.
        TextMeshProUGUI title = deathScreen.GetComponentInChildren<TextMeshProUGUI>(true);
        Transform parent = title != null ? title.transform.parent : deathScreen.transform;
        TMP_FontAsset font = title != null ? title.font : null;

        // Grouped under one object so the whole block can be moved at once.
        RectTransform group = RuntimeUI.CreateRect("RunSummary", parent);
        RuntimeUI.Place(group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 158f), new Vector2(900f, 110f));
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Create Run Summary");

        if (reasonProp.objectReferenceValue == null)
        {
            reasonProp.objectReferenceValue = CreateLine(group, "Reason", "You crashed!", font, 34f, 37f);
        }
        if (fareProp.objectReferenceValue == null)
        {
            fareProp.objectReferenceValue = CreateLine(group, "Fare", "FARE  R0", font, 32f, 0f);
        }
        if (bestProp.objectReferenceValue == null)
        {
            bestProp.objectReferenceValue = CreateLine(group, "Best", "BEST  R0", font, 32f, -37f);
        }

        created.Add("Run summary texts (Reason, Fare, Best) on the Death Screen");
    }

    private static TextMeshProUGUI CreateLine(RectTransform parent, string name, string placeholder,
        TMP_FontAsset font, float size, float y)
    {
        TextMeshProUGUI text = RuntimeUI.CreateText(name, parent, placeholder, size, font, Color.white);
        RuntimeUI.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(900f, 36f));
        return text;
    }

    // ---------------------------------------------------------------- Score popup

    private static void CreateScorePopupTemplate(SerializedObject so, TextMeshProUGUI scoreText, List<string> created)
    {
        SerializedProperty prop = so.FindProperty("scorePopupTemplate");
        if (prop.objectReferenceValue != null || scoreText == null) return;

        Transform parent = scoreText.transform.parent != null ? scoreText.transform.parent : scoreText.transform;
        TextMeshProUGUI popup = RuntimeUI.CreateText("ScorePopupTemplate", parent, "+R10", scoreText.fontSize,
            scoreText.font, new Color(0.35f, 1f, 0.45f), TextAlignmentOptions.Left);
        RuntimeUI.Place(popup.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(220f, 60f));
        // Left enabled so you can see it while positioning; the game hides it on start.
        Undo.RegisterCreatedObjectUndo(popup.gameObject, "Create Score Popup Template");

        prop.objectReferenceValue = popup;
        created.Add("Score popup template (next to the score; the game hides it on start)");
    }

    // ---------------------------------------------------------------- Touch controls

    private static void CreateTouchControls(SerializedObject so, UIController ui, TextMeshProUGUI scoreText, List<string> created)
    {
        SerializedProperty prop = so.FindProperty("mobileControls");
        if (prop.objectReferenceValue != null) return;

        Canvas canvas = ui.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogWarning("[Mzansi Rush] UIController is not on a Canvas; skipped the touch controls.");
            return;
        }

        Sprite rounded = SaveSprite("TouchRoundedRect", RuntimeUI.CreateRoundedRectTexture(true), RuntimeUI.RoundedRectBorder);
        Sprite circle = SaveSprite("TouchCircle", RuntimeUI.CreateCircleTexture(true), 0);
        Sprite arrow = SaveSprite("TouchArrow", RuntimeUI.CreateArrowTexture(true), 0);

        MobileControls controls = MobileControls.Build(canvas.transform, scoreText != null ? scoreText.font : null,
            rounded, circle, arrow);
        controls.transform.SetAsLastSibling(); // draw above the HUD
        Undo.RegisterCreatedObjectUndo(controls.gameObject, "Create Touch Controls");

        prop.objectReferenceValue = controls;
        Selection.activeGameObject = controls.gameObject;
        created.Add($"Touch controls (in the '{canvas.name}' canvas, sprites in {SpriteFolder})");
    }

    private static Sprite SaveSprite(string name, Texture2D texture, int border)
    {
        string path = $"{SpriteFolder}/{name}.png";

        // Never overwrite art you may have replaced.
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
        {
            Object.DestroyImmediate(texture);
            return existing;
        }

        Directory.CreateDirectory(SpriteFolder);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
