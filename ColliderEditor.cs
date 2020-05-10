using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SimpleJSON;
using UnityEngine;

using Object = UnityEngine.Object;
using Random = UnityEngine.Random;


/// <summary>
/// Collider Editor
/// By Acidbubbles and ProjectCanyon
/// Configures and customizes collisions (rigidbodies and colliders)
/// Source: https://github.com/acidbubbles/vam-collider-editor
/// </summary>
public class ColliderEditor : MVRScript
{
    private const string _saveExt = "colliders";

    private string _lastBrowseDir = SuperController.singleton.savesDir;

    private Dictionary<string, RigidbodyGroupModel> _rigidbodyGroups;
    private Dictionary<string, ColliderModel> _colliders;
    private Dictionary<string, AutoColliderModel> _autoColliders;
    private Dictionary<string, RigidbodyModel> _rigidbodies;

    private JSONStorableStringChooser _rigidbodyGroupsJson;
    private JSONStorableStringChooser _targetJson;
    private JSONStorableStringChooser _autoColliderJson;
    private JSONStorableStringChooser _rigidbodiesJson;

    private ColliderModel _lastSelectedCollider;
    private RigidbodyModel _lastSelectedRigidbody;
    private AutoColliderModel _lastSelectedAutoCollider;

    private ColliderModel _selectedCollider;
    private RigidbodyGroupModel _selectedGroup;
    private RigidbodyModel _selectedRigidbody;
    private AutoColliderModel _selectedAutoCollider;

    private UIDynamicPopup _rbGroupListUI;
    private UIDynamicPopup _ridigBodyList;
    private UIDynamicPopup _rbListUI;
    private UIDynamicPopup _autoColliderListUI;

    public override void Init()
    {
        try
        {
            BuildModels();
            BuildUI();
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(ColliderEditor)}.{nameof(Init)}: {e}");
        }
    }

    private void BuildUI()
    {
        var showPreviews = new JSONStorableBool("showPreviews", false, value =>
        {
            foreach (var colliderPair in _colliders)
                colliderPair.Value.ShowPreview = value;
        });
        RegisterBool(showPreviews);

        var showPreviewsToggle = CreateToggle(showPreviews);
        showPreviewsToggle.label = "Show Previews";

        var xRayPreviews = new JSONStorableBool("xRayPreviews", true, value =>
        {
            foreach (var colliderPair in _colliders)
                colliderPair.Value.XRayPreview = value;
        });
        RegisterBool(xRayPreviews);

        var xRayPreviewsToggle = CreateToggle(xRayPreviews);
        xRayPreviewsToggle.label = "Use XRay Previews";

        JSONStorableFloat previewOpacity = new JSONStorableFloat("previewOpacity", 0.001f, value =>
                   {
                       var alpha = ExponentialScale(value, 0.2f, 1f);
                       foreach (var colliderPair in _colliders)
                           colliderPair.Value.PreviewOpacity = alpha;
                   }, 0f, 1f);
        RegisterFloat(previewOpacity);
        CreateSlider(previewOpacity).label = "Preview Opacity";

        JSONStorableFloat selectedPreviewOpacity = new JSONStorableFloat("selectedPreviewOpacity", 0.3f, value =>
                   {
                       var alpha = ExponentialScale(value, 0.2f, 1f);
                       foreach (var colliderPair in _colliders)
                           colliderPair.Value.SelectedPreviewOpacity = alpha;
                   }, 0f, 1f);
        RegisterFloat(selectedPreviewOpacity);
        CreateSlider(selectedPreviewOpacity).label = "Selected Preview Opacity";

        var loadPresetUI = CreateButton("Load Preset");
        loadPresetUI.button.onClick.AddListener(() =>
        {
            if (_lastBrowseDir != null) SuperController.singleton.NormalizeMediaPath(_lastBrowseDir);
            SuperController.singleton.GetMediaPathDialog(HandleLoadPreset, _saveExt);
        });

        var savePresetUI = CreateButton("Save Preset");
        savePresetUI.button.onClick.AddListener(() =>
        {
            SuperController.singleton.NormalizeMediaPath(_lastBrowseDir);
            SuperController.singleton.GetMediaPathDialog(HandleSavePreset, _saveExt);

            var browser = SuperController.singleton.mediaFileBrowserUI;
            browser.SetTextEntry(true);
            browser.fileEntryField.text = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds + "." + _saveExt;
            browser.ActivateFileNameField();
        });

        var resetAllUI = CreateButton("Reset All");
        resetAllUI.button.onClick.AddListener(() =>
        {
            foreach (var colliderPair in _colliders)
                colliderPair.Value.ResetToInitial();
        });

        _rigidbodyGroupsJson = new JSONStorableStringChooser(
            "Rigidbody Groups",
            _rigidbodyGroups.Keys.ToList(),
            _rigidbodyGroups.Select(x => x.Value.Name).ToList(),
            _rigidbodyGroups.Keys.First(),
            "Rigidbody Groups");

        _rbGroupListUI = CreateScrollablePopup(_rigidbodyGroupsJson);
        _rbGroupListUI.popupPanelHeight = 400f;

        _rigidbodiesJson = new JSONStorableStringChooser(
            "Rigidbodies",
            new List<string>(),
            new List<string>(),
            "All",
            "Rigidbodies");

        _ridigBodyList = CreateScrollablePopup(_rigidbodiesJson);
        _ridigBodyList.popupPanelHeight = 400f;

        _targetJson = new JSONStorableStringChooser(
            "Colliders",
            new List<string>(),
            new List<string>(),
            "",
            "Colliders");

        _rbListUI = CreateScrollablePopup(_targetJson, true);
        _rbListUI.popupPanelHeight = 400f;

        var autoColliderPairs = _autoColliders.OrderBy(kvp => kvp.Key).ToList();
        _autoColliderJson = new JSONStorableStringChooser(
            "Auto Colliders",
            autoColliderPairs.Select(kvp => kvp.Key).ToList(),
            autoColliderPairs.Select(kvp => kvp.Value.Label).ToList(),
            "", "Auto Colliders"
        );
        _autoColliderListUI = CreateScrollablePopup(_autoColliderJson);
        _autoColliderListUI.popupPanelHeight = 400f;

        _rigidbodyGroupsJson.setCallbackFunction = groupId =>
        {
            _rigidbodyGroups.TryGetValue(groupId, out _selectedGroup);
            UpdateFilter();
        };

        _rigidbodiesJson.setCallbackFunction = rigidbodyId =>
        {
            _selectedRigidbody?.DestroyControls();
            _rigidbodies.TryGetValue(rigidbodyId, out _selectedRigidbody);
            UpdateFilter();
        };

        _targetJson.setCallbackFunction = colliderId =>
        {
            _colliders.TryGetValue(colliderId, out _selectedCollider);
            UpdateFilter();
        };

        _autoColliderJson.setCallbackFunction = autoColliderId =>
        {
            _autoColliders.TryGetValue(autoColliderId, out _selectedAutoCollider);
            UpdateFilter();
        };

        _rigidbodyGroups.TryGetValue("Head / Ears", out _selectedGroup);

        UpdateFilter();
    }

    private void SyncPopups()
    {
        _rigidbodyGroupsJson.popup.Toggle();
        _rigidbodyGroupsJson.popup.Toggle();

        _rbListUI.popup.Toggle();
        _rbListUI.popup.Toggle();

        _ridigBodyList.popup.Toggle();
        _ridigBodyList.popup.Toggle();
    }

    private void BuildModels()
    {
        var rigidbodyGroups = containingAtom.type == "Person"
        ? new List<RigidbodyGroupModel>
        {
            new RigidbodyGroupModel("All", @"^.+$"),
            new RigidbodyGroupModel("Head / Ears", @"^(head|lowerJaw|tongue|neck)"),
            new RigidbodyGroupModel("Left arm", @"^l(Shldr|ForeArm)"),
            new RigidbodyGroupModel("Left hand", @"^l(Index|Mid|Ring|Pinky|Thumb|Carpal|Hand)[0-9]?$"),
            new RigidbodyGroupModel("Right arm", @"^r(Shldr|ForeArm)"),
            new RigidbodyGroupModel("Right hand", @"^r(Index|Mid|Ring|Pinky|Thumb|Carpal|Hand)[0-9]?$"),
            new RigidbodyGroupModel("Chest", @"^(chest|AutoColliderFemaleAutoColliderschest)"),
            new RigidbodyGroupModel("Left breast", @"l((Pectoral)|Nipple)"),
            new RigidbodyGroupModel("Right breast", @"r((Pectoral)|Nipple)"),
            new RigidbodyGroupModel("Abdomen / Belly / Back", @"^(AutoColliderFemaleAutoColliders)?abdomen"),
            new RigidbodyGroupModel("Hip / Pelvis", @"^(AutoCollider)?(hip|pelvis)"),
            new RigidbodyGroupModel("Glute", @"^(AutoColliderFemaleAutoColliders)?[LR]Glute"),
            new RigidbodyGroupModel("Anus", @"^_JointA[rl]"),
            new RigidbodyGroupModel("Vagina", @"^_Joint(Gr|Gl|B)"),
            new RigidbodyGroupModel("Penis", @"^(Gen[1-3])|Testes"),
            new RigidbodyGroupModel("Left leg", @"^(AutoCollider(FemaleAutoColliders)?)?l(Thigh|Shin)"),
            new RigidbodyGroupModel("Left foot", @"^l(Foot|Toe|BigToe|SmallToe)"),
            new RigidbodyGroupModel("Right leg", @"^(AutoCollider(FemaleAutoColliders)?)?r(Thigh|Shin)"),
            new RigidbodyGroupModel("Right foot", @"^r(Foot|Toe|BigToe|SmallToe)"),
            new RigidbodyGroupModel("Other", @"^(?!.*).*$")
        }
        : new List<RigidbodyGroupModel>
        {
            new RigidbodyGroupModel("All", @"^.+$"),
        };

        // AutoColliders

        _autoColliders = containingAtom.GetComponentsInChildren<AutoCollider>()
            .Select(autoCollider => AutoColliderModel.Create(this, autoCollider))
            .ToDictionary(autoColliderModel => autoColliderModel.Id);

        var autoCollidersRigidBodies = new HashSet<Rigidbody>(_autoColliders.Values.SelectMany(x => x.GetRigidbodies()));
        var autoCollidersColliders = new HashSet<Collider>(_autoColliders.Values.SelectMany(x => x.GetColliders()));

        // Rigidbodies

        _rigidbodyGroups = rigidbodyGroups.ToDictionary(x => x.Id);

        _rigidbodies = containingAtom.GetComponentsInChildren<Rigidbody>(true)
            .Where(rigidbody => !autoCollidersRigidBodies.Contains(rigidbody))
            .Where(rigidbody => IsRigidbodyIncluded(rigidbody))
            .Select(rigidbody => RigidbodyModel.Create(this, rigidbody, rigidbodyGroups))
            .ToDictionary(x => x.Id);

        // Colliders

        var colliderQuery = containingAtom.GetComponentsInChildren<Collider>(true)
            .Where(collider => !autoCollidersColliders.Contains(collider))
            .Where(collider => IsColliderIncluded(collider));


        _colliders = new Dictionary<string, ColliderModel>();

        foreach (Collider collider in colliderQuery)
        {
            var model = ColliderModel.CreateTyped(this, collider, _rigidbodies);

            if (_colliders.ContainsKey(model.Id))
            {
                SuperController.LogError($"Duplicate collider Id {model.Id}");
                continue;
            }

            _colliders.Add(model.Id, model);
        }
    }

    private static bool IsColliderIncluded(Collider collider)
    {
        if (collider.name == "control") return false;
        if (collider.name == "object") return false;
        if (collider.name.Contains("Tool")) return false;
        if (collider.name.EndsWith("Control")) return false;
        if (collider.name.EndsWith("Link")) return false;
        if (collider.name.EndsWith("Trigger")) return false;
        if (collider.name.EndsWith("UI")) return false;
        if (collider.name.Contains("Ponytail")) return false;
        return true;
    }

    private static bool IsRigidbodyIncluded(Rigidbody rigidbody)
    {
        if (rigidbody.isKinematic) return false;
        if (rigidbody.name == "control") return false;
        if (rigidbody.name == "object") return false;
        if (rigidbody.name.EndsWith("Control")) return false;
        if (rigidbody.name.StartsWith("hairTool")) return false;
        if (rigidbody.name.EndsWith("Trigger")) return false;
        if (rigidbody.name.EndsWith("UI")) return false;
        if (rigidbody.name.Contains("Ponytail")) return false;
        return true;
    }

    private void UpdateFilter()
    {
        try
        {
            IEnumerable<RigidbodyModel> rigidbodies;
            IEnumerable<ColliderModel> colliders;

            // Rigidbody filtering

            if (_selectedGroup != null)
            {
                rigidbodies = _rigidbodies.Values.Where(x => x.Groups.Contains(_selectedGroup));
                colliders = _colliders.Values.Where(collider => collider.Rididbody != null && collider.Rididbody.Groups.Contains(_selectedGroup));
            }
            else
            {
                rigidbodies = _rigidbodies.Values;
                colliders = _colliders.Values;
            }

            if (_selectedGroup != null && _rigidbodyGroupsJson.choices.Contains(_selectedGroup.Id))
            {
                _rigidbodyGroupsJson.valNoCallback = _selectedGroup.Id;
            }
            else
            {
                _rigidbodyGroupsJson.valNoCallback = "All";
                _selectedGroup = null;
            }

            _rigidbodiesJson.choices = new[] { "All" }.Concat(rigidbodies.Select(x => x.Id)).ToList();
            _rigidbodiesJson.displayChoices = new[] { "All" }.Concat(rigidbodies.Select(x => x.Label)).ToList();


            if (_selectedRigidbody != null && _rigidbodiesJson.choices.Contains(_selectedRigidbody.Id))
            {
                _rigidbodiesJson.valNoCallback = _selectedRigidbody.Id;
            }
            else
            {
                _rigidbodiesJson.valNoCallback = "All";
                _selectedRigidbody = null;
            }

            // Collider filtering

            if (_selectedRigidbody != null) colliders = _colliders.Values.Where(collider => collider.Rididbody != null && collider.Rididbody == _selectedRigidbody);

            _targetJson.choices = colliders.Select(x => x.Id).ToList();
            _targetJson.displayChoices = colliders.Select(x => x.Label).ToList();

            if (_selectedCollider != null && _targetJson.choices.Contains(_selectedCollider.Id))
            {
                _targetJson.valNoCallback = _selectedCollider.Id;
            }
            else
            {
                var firstAvailableId = _targetJson.choices.FirstOrDefault();
                _targetJson.valNoCallback = firstAvailableId ?? string.Empty;
                if (!string.IsNullOrEmpty(firstAvailableId))
                    _colliders.TryGetValue(firstAvailableId, out _selectedCollider);
                else
                    _selectedCollider = null;
            }

            UpdateSelectedRigidbody();
            UpdateSelectedCollider();
            UpdateSelectedAutoCollider();
            SyncPopups();

        }
        catch (Exception e)
        {
            LogError(nameof(UpdateFilter), e.ToString());
        }
    }

    private void UpdateSelectedCollider()
    {
        if (_lastSelectedCollider != null)
            _lastSelectedCollider.Selected = false;

        if (_selectedCollider != null)
        {
            _selectedCollider.Selected = true;
            _lastSelectedCollider = _selectedCollider;
        }
    }

    private void UpdateSelectedRigidbody()
    {
        if (_lastSelectedRigidbody != null)
            _lastSelectedRigidbody.Selected = false;

        if (_selectedRigidbody != null)
        {
            _selectedRigidbody.Selected = true;
            _lastSelectedRigidbody = _selectedRigidbody;
        }
    }

    private void UpdateSelectedAutoCollider()
    {
        if (_lastSelectedAutoCollider != null)
            _lastSelectedAutoCollider.Selected = false;

        if (_selectedAutoCollider != null)
        {
            _selectedAutoCollider.Selected = true;
            _lastSelectedAutoCollider = _selectedAutoCollider;
        }
    }

    private void LogError(string method, string message) => SuperController.LogError($"{nameof(ColliderEditor)}.{method}: {message}");

    private void HandleLoadPreset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        _lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new[] { '/', '\\' })) + @"\";

        LoadFromJson((JSONClass)LoadJSON(path));
    }

    private void LoadFromJson(JSONClass jsonClass)
    {
        var collidersJsonClass = jsonClass["colliders"].AsObject;
        foreach (string colliderId in collidersJsonClass.Keys)
        {
            ColliderModel colliderModel;
            if (_colliders.TryGetValue(colliderId, out colliderModel))
                colliderModel.LoadJson(collidersJsonClass[colliderId].AsObject);
        }

        var rigidbodiesJsonClass = jsonClass["rigidbodies"].AsObject;
        foreach (string rigidbodyId in rigidbodiesJsonClass.Keys)
        {
            RigidbodyModel rigidbodyModel;
            if (_rigidbodies.TryGetValue(rigidbodyId, out rigidbodyModel))
                rigidbodyModel.LoadJson(rigidbodiesJsonClass[rigidbodyId].AsObject);
        }
    }

    private void HandleSavePreset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        _lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new[] { '/', '\\' })) + @"\";

        if (!path.ToLower().EndsWith($".{_saveExt}"))
            path += $".{_saveExt}";

        var presetJsonClass = new JSONClass();
        AppendJson(presetJsonClass);
        SaveJSON(presetJsonClass, path);
    }

    public void OnDestroy()
    {
        if (_colliders == null) return;
        try
        {
            foreach (var colliderModelPair in _colliders)
                colliderModelPair.Value.DestroyPreview();
        }
        catch (Exception e)
        {
            LogError(nameof(OnDestroy), e.ToString());
        }
    }

    public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
    {
        var jsonClass = base.GetJSON(includePhysical, includeAppearance, forceStore);

        needsStore = true;

        AppendJson(jsonClass);

        return jsonClass;
    }

    private void AppendJson(JSONClass jsonClass)
    {
        var colliders = new JSONClass();
        foreach (var colliderPairs in _colliders)
            colliderPairs.Value.AppendJson(colliders);
        jsonClass.Add("colliders", colliders);

        var rigidbodies = new JSONClass();
        foreach (var rigidbodyPair in _rigidbodies)
            rigidbodyPair.Value.AppendJson(rigidbodies);
        jsonClass.Add("rigidbodies", rigidbodies);
    }

    private float ExponentialScale(float inputValue, float midValue, float maxValue)
    {
        var m = maxValue / midValue;
        var c = Mathf.Log(Mathf.Pow(m - 1, 2));
        var b = maxValue / (Mathf.Exp(c) - 1);
        var a = -1 * b;
        return a + b * Mathf.Exp(c * inputValue);
    }

    private void FixedUpdate()
    {
        foreach (var colliderPair in _colliders)
        {
            colliderPair.Value.UpdateControls();
            colliderPair.Value.UpdatePreview();
        }
    }

    public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
    {
        base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);

        try
        {
            LoadFromJson(jc);
        }
        catch (Exception exc)
        {
            LogError(nameof(RestoreFromJSON), exc.ToString());
        }
    }
}

public static class ColorExtensions
{
    public static Color ToColor(this string value)
    {
        Color color;
        ColorUtility.TryParseHtmlString(value, out color);
        color.a = 0.005f;
        return color;
    }
}

public static class MaterialHelper
{
    private static Queue<Material> _materials;

    public static Material GetNextMaterial()
    {
        if (_materials == null)
        {
            var materials = new List<Material>();
            materials.Add(CreateMaterial("#800000".ToColor()));
            materials.Add(CreateMaterial("#8B0000".ToColor()));
            materials.Add(CreateMaterial("#A52A2A".ToColor()));
            materials.Add(CreateMaterial("#B22222".ToColor()));
            materials.Add(CreateMaterial("#DC143C".ToColor()));
            materials.Add(CreateMaterial("#FF0000".ToColor()));
            materials.Add(CreateMaterial("#FF6347".ToColor()));
            materials.Add(CreateMaterial("#FF7F50".ToColor()));
            materials.Add(CreateMaterial("#CD5C5C".ToColor()));
            materials.Add(CreateMaterial("#F08080".ToColor()));
            materials.Add(CreateMaterial("#E9967A".ToColor()));
            materials.Add(CreateMaterial("#FA8072".ToColor()));
            materials.Add(CreateMaterial("#FFA07A".ToColor()));
            materials.Add(CreateMaterial("#FF4500".ToColor()));
            materials.Add(CreateMaterial("#FF8C00".ToColor()));
            materials.Add(CreateMaterial("#FFA500".ToColor()));
            materials.Add(CreateMaterial("#FFD700".ToColor()));
            materials.Add(CreateMaterial("#B8860B".ToColor()));
            materials.Add(CreateMaterial("#DAA520".ToColor()));
            materials.Add(CreateMaterial("#EEE8AA".ToColor()));
            materials.Add(CreateMaterial("#BDB76B".ToColor()));
            materials.Add(CreateMaterial("#F0E68C".ToColor()));
            materials.Add(CreateMaterial("#808000".ToColor()));
            materials.Add(CreateMaterial("#FFFF00".ToColor()));
            materials.Add(CreateMaterial("#9ACD32".ToColor()));
            materials.Add(CreateMaterial("#556B2F".ToColor()));
            materials.Add(CreateMaterial("#6B8E23".ToColor()));
            materials.Add(CreateMaterial("#7CFC00".ToColor()));
            materials.Add(CreateMaterial("#7FFF00".ToColor()));
            materials.Add(CreateMaterial("#ADFF2F".ToColor()));
            materials.Add(CreateMaterial("#006400".ToColor()));
            materials.Add(CreateMaterial("#008000".ToColor()));
            materials.Add(CreateMaterial("#228B22".ToColor()));
            materials.Add(CreateMaterial("#00FF00".ToColor()));
            materials.Add(CreateMaterial("#32CD32".ToColor()));
            materials.Add(CreateMaterial("#90EE90".ToColor()));
            materials.Add(CreateMaterial("#98FB98".ToColor()));
            materials.Add(CreateMaterial("#8FBC8F".ToColor()));
            materials.Add(CreateMaterial("#00FA9A".ToColor()));
            materials.Add(CreateMaterial("#00FF7F".ToColor()));
            materials.Add(CreateMaterial("#2E8B57".ToColor()));
            materials.Add(CreateMaterial("#66CDAA".ToColor()));
            materials.Add(CreateMaterial("#3CB371".ToColor()));
            materials.Add(CreateMaterial("#20B2AA".ToColor()));
            materials.Add(CreateMaterial("#2F4F4F".ToColor()));
            materials.Add(CreateMaterial("#008080".ToColor()));
            materials.Add(CreateMaterial("#008B8B".ToColor()));
            materials.Add(CreateMaterial("#00FFFF".ToColor()));
            materials.Add(CreateMaterial("#00FFFF".ToColor()));
            materials.Add(CreateMaterial("#E0FFFF".ToColor()));
            materials.Add(CreateMaterial("#00CED1".ToColor()));
            materials.Add(CreateMaterial("#40E0D0".ToColor()));
            materials.Add(CreateMaterial("#48D1CC".ToColor()));
            materials.Add(CreateMaterial("#AFEEEE".ToColor()));
            materials.Add(CreateMaterial("#7FFFD4".ToColor()));
            materials.Add(CreateMaterial("#B0E0E6".ToColor()));
            materials.Add(CreateMaterial("#5F9EA0".ToColor()));
            materials.Add(CreateMaterial("#4682B4".ToColor()));
            materials.Add(CreateMaterial("#6495ED".ToColor()));
            materials.Add(CreateMaterial("#00BFFF".ToColor()));
            materials.Add(CreateMaterial("#1E90FF".ToColor()));
            materials.Add(CreateMaterial("#ADD8E6".ToColor()));
            materials.Add(CreateMaterial("#87CEEB".ToColor()));
            materials.Add(CreateMaterial("#87CEFA".ToColor()));
            materials.Add(CreateMaterial("#191970".ToColor()));
            materials.Add(CreateMaterial("#000080".ToColor()));
            materials.Add(CreateMaterial("#00008B".ToColor()));
            materials.Add(CreateMaterial("#0000CD".ToColor()));
            materials.Add(CreateMaterial("#0000FF".ToColor()));
            materials.Add(CreateMaterial("#4169E1".ToColor()));
            materials.Add(CreateMaterial("#8A2BE2".ToColor()));
            materials.Add(CreateMaterial("#4B0082".ToColor()));
            materials.Add(CreateMaterial("#483D8B".ToColor()));
            materials.Add(CreateMaterial("#6A5ACD".ToColor()));
            materials.Add(CreateMaterial("#7B68EE".ToColor()));
            materials.Add(CreateMaterial("#9370DB".ToColor()));
            materials.Add(CreateMaterial("#8B008B".ToColor()));
            materials.Add(CreateMaterial("#9400D3".ToColor()));
            materials.Add(CreateMaterial("#9932CC".ToColor()));
            materials.Add(CreateMaterial("#BA55D3".ToColor()));
            materials.Add(CreateMaterial("#800080".ToColor()));
            materials.Add(CreateMaterial("#D8BFD8".ToColor()));
            materials.Add(CreateMaterial("#DDA0DD".ToColor()));
            materials.Add(CreateMaterial("#EE82EE".ToColor()));
            materials.Add(CreateMaterial("#FF00FF".ToColor()));
            materials.Add(CreateMaterial("#DA70D6".ToColor()));
            materials.Add(CreateMaterial("#C71585".ToColor()));
            materials.Add(CreateMaterial("#DB7093".ToColor()));
            materials.Add(CreateMaterial("#FF1493".ToColor()));
            materials.Add(CreateMaterial("#FF69B4".ToColor()));
            materials.Add(CreateMaterial("#FFB6C1".ToColor()));
            materials.Add(CreateMaterial("#FFC0CB".ToColor()));
            materials.Add(CreateMaterial("#FAEBD7".ToColor()));
            materials.Add(CreateMaterial("#F5F5DC".ToColor()));
            materials.Add(CreateMaterial("#FFE4C4".ToColor()));
            materials.Add(CreateMaterial("#FFEBCD".ToColor()));
            materials.Add(CreateMaterial("#F5DEB3".ToColor()));
            materials.Add(CreateMaterial("#FFF8DC".ToColor()));
            materials.Add(CreateMaterial("#FFFACD".ToColor()));
            materials.Add(CreateMaterial("#FAFAD2".ToColor()));
            materials.Add(CreateMaterial("#FFFFE0".ToColor()));
            materials.Add(CreateMaterial("#8B4513".ToColor()));
            materials.Add(CreateMaterial("#A0522D".ToColor()));
            materials.Add(CreateMaterial("#D2691E".ToColor()));
            materials.Add(CreateMaterial("#CD853F".ToColor()));
            materials.Add(CreateMaterial("#F4A460".ToColor()));
            materials.Add(CreateMaterial("#DEB887".ToColor()));
            materials.Add(CreateMaterial("#D2B48C".ToColor()));
            materials.Add(CreateMaterial("#BC8F8F".ToColor()));
            materials.Add(CreateMaterial("#FFE4B5".ToColor()));
            materials.Add(CreateMaterial("#FFDEAD".ToColor()));
            materials.Add(CreateMaterial("#FFDAB9".ToColor()));
            materials.Add(CreateMaterial("#FFE4E1".ToColor()));
            materials.Add(CreateMaterial("#FFF0F5".ToColor()));

            _materials = new Queue<Material>(materials.OrderBy(x => Random.Range(-1, 2)));
        }

        Material current;
        _materials.Enqueue(current = _materials.Dequeue());
        return current;
    }

    private static Material CreateMaterial(Color color)
    {
        var material = new Material(Shader.Find("Battlehub/RTGizmos/Handles")) { color = color };
        material.SetFloat("_Offset", 1f);
        material.SetFloat("_MinAlpha", 1f);

        return material;
    }
}

public abstract class ColliderModel<T> : ColliderModel where T : Collider
{
    protected T Collider { get; }

    protected ColliderModel(MVRScript parent, T collider, string label)
        : base(parent, collider.Uuid(), label)
    {
        Collider = collider;
    }

    public override void CreatePreview()
    {
        var preview = DoCreatePreview();

        preview.GetComponent<Renderer>().material = MaterialHelper.GetNextMaterial();
        foreach (var c in preview.GetComponents<Collider>())
        {
            c.enabled = false;
            Object.Destroy(c);
        }

        preview.transform.SetParent(Collider.transform, false);

        Preview = preview;

        DoUpdatePreview();
        SetSelected(Selected);
    }
}

public interface IModel
{
    string Id { get; }
    string Label { get; }
}

public abstract class ColliderModel : IModel
{
    private float _previewOpacity;

    private bool _selected;

    private float _selectedPreviewOpacity;
    private JSONStorableBool _xRayStorable;

    private bool _showPreview;
    protected MVRScript Parent { get; }

    public string Id { get; }
    public string Label { get; }
    public RigidbodyModel Rididbody { get; set; }

    public GameObject Preview { get; protected set; }
    public List<UIDynamic> Controls { get; private set; }

    public bool Selected
    {
        get { return _selected; }
        set
        {
            if (_selected != value)
            {
                SetSelected(value);
                _selected = value;
            }
        }
    }

    public float SelectedPreviewOpacity
    {
        get { return _selectedPreviewOpacity; }
        set
        {
            if (Mathf.Approximately(value, _selectedPreviewOpacity))
                return;

            _selectedPreviewOpacity = value;

            if (Preview != null && _selected)
            {
                var previewRenderer = Preview.GetComponent<Renderer>();
                var color = previewRenderer.material.color;
                color.a = _selectedPreviewOpacity;
                previewRenderer.material.color = color;
                previewRenderer.enabled = false;
                previewRenderer.enabled = true;
            }
        }
    }

    public float PreviewOpacity
    {
        get { return _previewOpacity; }
        set
        {
            if (Mathf.Approximately(value, _previewOpacity))
                return;

            _previewOpacity = value;

            if (Preview != null && !_selected)
            {
                var previewRenderer = Preview.GetComponent<Renderer>();
                var color = previewRenderer.material.color;
                color.a = _previewOpacity;
                previewRenderer.material.color = color;

            }
        }
    }

    public bool ShowPreview
    {
        get { return _showPreview; }
        set
        {
            _showPreview = value;

            if (_showPreview)
                CreatePreview();
            else
                DestroyPreview();
        }
    }

    private bool _xRayPreview;
    public bool XRayPreview
    {
        get { return _xRayPreview; }
        set
        {
            _xRayPreview = value;

            if (Preview != null)
            {
                var previewRenderer = Preview.GetComponent<Renderer>();
                var material = previewRenderer.material;

                if (_xRayPreview)
                {
                    material.shader = Shader.Find("Battlehub/RTGizmos/Handles");
                    material.SetFloat("_Offset", 1f);
                    material.SetFloat("_MinAlpha", 1f);
                }
                else
                {
                    material.shader = Shader.Find("Standard");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                }

                previewRenderer.material = material;

                if (_xRayStorable != null)
                    _xRayStorable.valNoCallback = value;
            }
        }
    }

    protected ColliderModel(MVRScript parent, string id, string label)
    {
        Parent = parent;

        Id = id;
        Label = label;
    }

    public static ColliderModel CreateTyped(MVRScript parent, Collider collider, Dictionary<string, RigidbodyModel> rigidbodies)
    {
        ColliderModel typed;

        if (collider is SphereCollider)
            typed = new SphereColliderModel(parent, (SphereCollider)collider);
        else if (collider is BoxCollider)
            typed = new BoxColliderModel(parent, (BoxCollider)collider);
        else if (collider is CapsuleCollider)
            typed = new CapsuleColliderModel(parent, (CapsuleCollider)collider);
        else
            throw new InvalidOperationException("Unsupported collider type");

        if (collider.attachedRigidbody != null)
        {
            RigidbodyModel rigidbodyModel;
            if (rigidbodies.TryGetValue(collider.attachedRigidbody.Uuid(), out rigidbodyModel))
            {
                typed.Rididbody = rigidbodyModel;
                if (rigidbodyModel.Colliders == null)
                    rigidbodyModel.Colliders = new List<ColliderModel> { typed };
                else
                    rigidbodyModel.Colliders.Add(typed);
            }
        }

        return typed;
    }

    public void CreateControls()
    {
        DestroyControls();

        var controls = new List<UIDynamic>();

        _xRayStorable = new JSONStorableBool("xRayPreview", true, (bool value) => { XRayPreview = value; });

        var xRayToggle = Parent.CreateToggle(_xRayStorable, true);
        xRayToggle.label = "XRay Preview";

        var resetUi = Parent.CreateButton("Reset Collider", true);
        resetUi.button.onClick.AddListener(ResetToInitial);

        controls.Add(xRayToggle);
        controls.Add(resetUi);
        controls.AddRange(DoCreateControls());

        Controls = controls;
    }

    public abstract IEnumerable<UIDynamic> DoCreateControls();

    public virtual void DestroyControls()
    {
        if (Controls == null)
            return;

        foreach (var adjustmentJson in Controls)
            Object.Destroy(adjustmentJson.gameObject);

        Controls.Clear();
    }

    public virtual void DestroyPreview()
    {
        if (Preview != null)
        {
            Object.Destroy(Preview);
            Preview = null;
        }
    }

    public abstract void CreatePreview();

    protected abstract GameObject DoCreatePreview();

    public void UpdatePreview()
    {
        if (_showPreview)
            DoUpdatePreview();
    }

    protected abstract void DoUpdatePreview();

    public void UpdateControls()
    {
        DoUpdateControls();
    }

    protected abstract void DoUpdateControls();

    protected virtual void SetSelected(bool value)
    {
        if (Preview != null)
        {
            var previewRenderer = Preview.GetComponent<Renderer>();
            var color = previewRenderer.material.color;
            color.a = value ? _selectedPreviewOpacity : _previewOpacity;
            previewRenderer.material.color = color;
        }

        if (value)
            CreateControls();
        else
            DestroyControls();
    }

    public void AppendJson(JSONClass parent)
    {
        parent.Add(Id, DoGetJson());
    }

    public void LoadJson(JSONClass jsonClass)
    {
        DoLoadJson(jsonClass);
        DoUpdatePreview();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected abstract void DoLoadJson(JSONClass jsonClass);

    public abstract JSONClass DoGetJson();

    public void ResetToInitial()
    {
        DoResetToInitial();
        DoUpdatePreview();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected abstract void DoResetToInitial();

    protected abstract bool DeviatesFromInitial();

    public override string ToString() => Id;
}

public class AutoColliderModel : IModel
{
    public static AutoColliderModel Create(MVRScript script, AutoCollider autoCollider)
    {
        return new AutoColliderModel(script, autoCollider, autoCollider.name);
    }

    private JSONStorableFloat _autoRadiusBufferFloat;
    private JSONStorableFloat _autoRadiusMultiplierFloat;

    private readonly float _initialAutoRadiusBuffer;
    private readonly float _initialAutoRadiusMultiplier;

    private bool _selected;
    private readonly MVRScript _script;
    private readonly AutoCollider _autoCollider;

    public string Id { get; set; }
    public string Label { get; set; }

    public List<UIDynamic> Controls { get; private set; }

    public bool Selected
    {
        get { return _selected; }
        set
        {
            if (_selected != value)
            {
                SetSelected(value);
                _selected = value;
            }
        }
    }

    public AutoColliderModel(MVRScript script, AutoCollider autoCollider, string label)
    {
        _script = script;
        _autoCollider = autoCollider;
        _initialAutoRadiusBuffer = autoCollider.autoRadiusBuffer;
        _initialAutoRadiusMultiplier = autoCollider.autoRadiusMultiplier;
        Id = autoCollider.Uuid();
        if (label.StartsWith("AutoColliderAutoColliders"))
            Label = label.Substring("AutoColliderAutoColliders".Length);
        else if (label.StartsWith("AutoColliderFemaleAutoColliders"))
            Label = label.Substring("AutoColliderFemaleAutoColliders".Length);
        else if (label.StartsWith("AutoCollider"))
            Label = label.Substring("AutoCollider".Length);
        else
            Label = label;
    }

    protected virtual void SetSelected(bool value)
    {
        if (value)
            CreateControls();
        else
            DestroyControls();
    }

    public void CreateControls()
    {
        DestroyControls();

        var controls = new List<UIDynamic>();

        var resetUi = _script.CreateButton("Reset AutoCollider", true);
        resetUi.button.onClick.AddListener(ResetToInitial);

        controls.Add(resetUi);
        controls.AddRange(DoCreateControls());

        Controls = controls;
    }

    public IEnumerable<UIDynamic> DoCreateControls()
    {
        yield return _script.CreateFloatSlider(_autoRadiusBufferFloat = new JSONStorableFloat("autoRadiusBuffer", _autoCollider.autoRadiusBuffer, value =>
        {
            _autoCollider.autoRadiusBuffer = value;
        }, 0f, _initialAutoRadiusBuffer * 4f, false).WithDefault(_initialAutoRadiusBuffer), "Auto Radius Buffer");

        yield return _script.CreateFloatSlider(_autoRadiusMultiplierFloat = new JSONStorableFloat("autoRadiusMultiplier", _autoCollider.autoRadiusMultiplier, value =>
        {
            _autoCollider.autoRadiusMultiplier = value;
        }, 0f, _initialAutoRadiusMultiplier * 4f, false).WithDefault(_initialAutoRadiusMultiplier), "Auto Radius Multiplier");
    }

    public virtual void DestroyControls()
    {
        if (Controls == null)
            return;

        foreach (var adjustmentJson in Controls)
            Object.Destroy(adjustmentJson.gameObject);

        Controls.Clear();
    }

    public void ResetToInitial()
    {
        DoResetToInitial();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected void DoResetToInitial()
    {
        _autoCollider.autoRadiusBuffer = _initialAutoRadiusBuffer;
    }

    public IEnumerable<Collider> GetColliders()
    {
        if (_autoCollider.hardCollider != null) yield return _autoCollider.hardCollider;
        if (_autoCollider.jointCollider != null) yield return _autoCollider.jointCollider;
    }

    public IEnumerable<Rigidbody> GetRigidbodies()
    {
        if (_autoCollider.jointRB != null) yield return _autoCollider.jointRB;
        if (_autoCollider.kinematicRB != null) yield return _autoCollider.kinematicRB;
    }
}

public class RigidbodyModel : IModel
{
    private readonly bool _initialEnabled;
    private readonly Rigidbody _rigidbody;

    private readonly MVRScript _script;

    private List<UIDynamic> _controls;

    private bool _selected;

    public string Id { get; set; }
    public string Name { get; set; }
    public string Label { get; set; }
    public List<RigidbodyGroupModel> Groups { get; set; }
    public List<ColliderModel> Colliders { get; set; }

    public bool Selected
    {
        get { return _selected; }
        set
        {
            if (_selected != value)
            {
                SetSelected(value);
                _selected = value;
            }
        }
    }

    public RigidbodyModel(MVRScript script, Rigidbody rigidbody, string label)
    {
        _script = script;
        _rigidbody = rigidbody;

        Id = rigidbody.Uuid();
        Name = rigidbody.name;
        Label = label;

        _initialEnabled = rigidbody.detectCollisions;
    }

    public static RigidbodyModel Create(MVRScript script, Rigidbody rigidbody, IEnumerable<RigidbodyGroupModel> groups)
    {
        var model = new RigidbodyModel(script, rigidbody, rigidbody.name);
        model.Groups = groups
            .Where(category => category.Pattern.IsMatch(rigidbody.name))
            .ToList();
        return model;
    }

    public override string ToString() => $"{Id}_{Name}";

    private void SetSelected(bool value)
    {
        if (value)
            CreateControls();
        else
            DestroyControls();
    }

    public void CreateControls()
    {
        DestroyControls();

        var controls = new List<UIDynamic>();

        var resetUi = _script.CreateButton("Reset Rigidbody");
        resetUi.button.onClick.AddListener(ResetToInitial);

        var enabledToggleJsf = new JSONStorableBool("enabled", _rigidbody.detectCollisions, value => { _rigidbody.detectCollisions = value; });
        var enabledToggle = _script.CreateToggle(enabledToggleJsf);
        enabledToggle.label = "Detect Collisions";

        controls.Add(resetUi);
        controls.Add(enabledToggle);

        _controls = controls;
    }

    public virtual void DestroyControls()
    {
        if (_controls == null)
            return;

        foreach (var control in _controls)
            Object.Destroy(control.gameObject);

        _controls.Clear();
    }

    public void AppendJson(JSONClass parent)
    {
        parent.Add(Id, DoGetJson());
    }

    public void LoadJson(JSONClass jsonClass)
    {
        DoLoadJson(jsonClass);

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    private void DoLoadJson(JSONClass jsonClass)
    {
        _rigidbody.detectCollisions = jsonClass["detectCollisions"].AsBool;
    }

    public JSONClass DoGetJson()
    {
        var jsonClass = new JSONClass();
        jsonClass["detectCollisions"].AsBool = _rigidbody.detectCollisions;
        return jsonClass;
    }

    public void ResetToInitial()
    {
        DoResetToInitial();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected void DoResetToInitial()
    {
        _rigidbody.detectCollisions = _initialEnabled;
    }

    protected bool DeviatesFromInitial() => _rigidbody.detectCollisions != _initialEnabled;
}

public class RigidbodyGroupModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Regex Pattern { get; set; }

    public RigidbodyGroupModel(string name, string pattern)
    {
        Id = name;
        Name = name;
        Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    }
}

public class CapsuleColliderModel : ColliderModel<CapsuleCollider>
{
    private JSONStorableFloat _centerXStorableFloat;
    private JSONStorableFloat _centerYStorableFloat;
    private JSONStorableFloat _centerZStorableFloat;
    private JSONStorableFloat _heightStorableFloat;
    private JSONStorableFloat _radiusStorableFloat;

    public float InitialRadius { get; set; }
    public float InitialHeight { get; set; }
    public Vector3 InitialCenter { get; set; }

    public CapsuleColliderModel(MVRScript parent, CapsuleCollider collider)
        : base(parent, collider, collider.name)
    {
        InitialRadius = collider.radius;
        InitialHeight = collider.height;
        InitialCenter = collider.center;
    }

    public override IEnumerable<UIDynamic> DoCreateControls()
    {
        yield return Parent.CreateFloatSlider(_radiusStorableFloat = new JSONStorableFloat("radius", Collider.radius, value =>
        {
            Collider.radius = value;
            DoUpdatePreview();
        }, 0f, InitialRadius * 4f, false).WithDefault(InitialRadius), "Radius");

        yield return Parent.CreateFloatSlider(_heightStorableFloat = new JSONStorableFloat("height", Collider.height, value =>
        {
            Collider.height = value;
            DoUpdatePreview();
        }, 0f, InitialHeight * 4f, false).WithDefault(InitialHeight), "Height");

        yield return Parent.CreateFloatSlider(_centerXStorableFloat = new JSONStorableFloat("centerX", Collider.center.x, value =>
        {
            var center = Collider.center;
            center.x = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.x), "Center.X");

        yield return Parent.CreateFloatSlider(_centerYStorableFloat = new JSONStorableFloat("centerY", Collider.center.y, value =>
        {
            var center = Collider.center;
            center.y = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.y), "Center.Y");

        yield return Parent.CreateFloatSlider(_centerZStorableFloat = new JSONStorableFloat("centerZ", Collider.center.z, value =>
        {
            var center = Collider.center;
            center.z = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.z), "Center.Z");
    }

    protected override void DoLoadJson(JSONClass jsonClass)
    {
        Collider.radius = jsonClass["radius"].AsFloat;
        Collider.height = jsonClass["height"].AsFloat;

        var center = Collider.center;
        center.x = jsonClass["centerX"].AsFloat;
        center.y = jsonClass["centerY"].AsFloat;
        center.z = jsonClass["centerZ"].AsFloat;
        Collider.center = center;
    }

    public override JSONClass DoGetJson()
    {
        var jsonClass = new JSONClass();
        jsonClass["radius"].AsFloat = Collider.radius;
        jsonClass["height"].AsFloat = Collider.height;
        jsonClass["centerX"].AsFloat = Collider.center.x;
        jsonClass["centerY"].AsFloat = Collider.center.y;
        jsonClass["centerZ"].AsFloat = Collider.center.z;
        return jsonClass;
    }

    protected override void DoResetToInitial()
    {
        Collider.radius = InitialRadius;
        Collider.height = InitialHeight;
        Collider.center = InitialCenter;
    }

    protected override bool DeviatesFromInitial() =>
        !Mathf.Approximately(InitialRadius, Collider.radius) ||
        !Mathf.Approximately(InitialHeight, Collider.height) ||
        InitialCenter != Collider.center; // Vector3 has built in epsilon equality checks

    protected override GameObject DoCreatePreview() => GameObject.CreatePrimitive(PrimitiveType.Capsule);

    protected override void DoUpdatePreview()
    {
        if (Preview == null) return;

        float size = Collider.radius * 2;
        float height = Collider.height / 2;
        Preview.transform.localScale = new Vector3(size, height, size);
        if (Collider.direction == 0)
            Preview.transform.localRotation = Quaternion.AngleAxis(90, Vector3.forward);
        else if (Collider.direction == 2)
            Preview.transform.localRotation = Quaternion.AngleAxis(90, Vector3.right);
        Preview.transform.localPosition = Collider.center;
    }

    protected override void DoUpdateControls()
    {
        if (_radiusStorableFloat != null)
            _radiusStorableFloat.valNoCallback = Collider.radius;
        if (_heightStorableFloat != null)
            _heightStorableFloat.valNoCallback = Collider.height;
        if (_centerXStorableFloat != null)
            _centerXStorableFloat.valNoCallback = Collider.center.x;
        if (_centerYStorableFloat != null)
            _centerYStorableFloat.valNoCallback = Collider.center.y;
        if (_centerZStorableFloat != null)
            _centerZStorableFloat.valNoCallback = Collider.center.z;
    }
}

public class SphereColliderModel : ColliderModel<SphereCollider>
{
    private JSONStorableFloat _centerXStorableFloat;
    private JSONStorableFloat _centerYStorableFloat;
    private JSONStorableFloat _centerZStorableFloat;
    private JSONStorableFloat _radiusStorableFloat;

    public float InitialRadius { get; set; }
    public Vector3 InitialCenter { get; set; }

    public SphereColliderModel(MVRScript parent, SphereCollider collider)
        : base(parent, collider, collider.name)
    {
        InitialRadius = collider.radius;
        InitialCenter = collider.center;
    }

    protected override GameObject DoCreatePreview() => GameObject.CreatePrimitive(PrimitiveType.Sphere);

    protected override void DoUpdatePreview()
    {
        if (Preview == null) return;

        Preview.transform.localScale = Vector3.one * (Collider.radius * 2);
        Preview.transform.localPosition = Collider.center;
    }

    protected override void DoUpdateControls()
    {
        if (_radiusStorableFloat != null)
            _radiusStorableFloat.valNoCallback = Collider.radius;
        if (_centerXStorableFloat != null)
            _centerXStorableFloat.valNoCallback = Collider.center.x;
        if (_centerYStorableFloat != null)
            _centerYStorableFloat.valNoCallback = Collider.center.y;
        if (_centerZStorableFloat != null)
            _centerZStorableFloat.valNoCallback = Collider.center.z;
    }

    protected override void DoLoadJson(JSONClass jsonClass)
    {
        Collider.radius = jsonClass["radius"].AsFloat;

        var center = Collider.center;
        center.x = jsonClass["centerX"].AsFloat;
        center.y = jsonClass["centerY"].AsFloat;
        center.z = jsonClass["centerZ"].AsFloat;
        Collider.center = center;
    }

    public override JSONClass DoGetJson()
    {
        var jsonClass = new JSONClass();

        jsonClass["radius"].AsFloat = Collider.radius;

        jsonClass["centerX"].AsFloat = Collider.center.x;
        jsonClass["centerY"].AsFloat = Collider.center.y;
        jsonClass["centerZ"].AsFloat = Collider.center.z;

        return jsonClass;
    }

    protected override void DoResetToInitial()
    {
        Collider.radius = InitialRadius;
        Collider.center = InitialCenter;
    }

    public override IEnumerable<UIDynamic> DoCreateControls()
    {
        yield return Parent.CreateFloatSlider(_radiusStorableFloat = new JSONStorableFloat("radius", Collider.radius, value =>
        {
            Collider.radius = value;
            DoUpdatePreview();
        }, 0f, InitialRadius * 4f, false).WithDefault(InitialRadius), "Radius");

        yield return Parent.CreateFloatSlider(_centerXStorableFloat = new JSONStorableFloat("centerX", Collider.center.x, value =>
        {
            var center = Collider.center;
            center.x = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.x), "Center.X");

        yield return Parent.CreateFloatSlider(_centerYStorableFloat = new JSONStorableFloat("centerY", Collider.center.y, value =>
        {
            var center = Collider.center;
            center.y = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.y), "Center.Y");

        yield return Parent.CreateFloatSlider(_centerZStorableFloat = new JSONStorableFloat("centerZ", Collider.center.z, value =>
        {
            var center = Collider.center;
            center.z = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.z), "Center.Z");
    }

    protected override bool DeviatesFromInitial() =>
        !Mathf.Approximately(InitialRadius, Collider.radius) ||
        InitialCenter != Collider.center; // Vector3 has built in epsilon equality checks
}

public class BoxColliderModel : ColliderModel<BoxCollider>
{
    private JSONStorableFloat _centerXStorableFloat;
    private JSONStorableFloat _centerYStorableFloat;
    private JSONStorableFloat _centerZStorableFloat;
    private JSONStorableFloat _sizeXStorableFloat;
    private JSONStorableFloat _sizeYStorableFloat;
    private JSONStorableFloat _sizeZStorableFloat;

    public Vector3 InitialSize { get; set; }
    public Vector3 InitialCenter { get; set; }

    public BoxColliderModel(MVRScript parent, BoxCollider collider)
        : base(parent, collider, collider.name)
    {
        InitialSize = collider.size;
        InitialCenter = collider.center;
    }

    protected override GameObject DoCreatePreview() => GameObject.CreatePrimitive(PrimitiveType.Cube);

    protected override void DoUpdatePreview()
    {
        if (Preview == null) return;

        Preview.transform.localScale = Collider.size;
        Preview.transform.localPosition = Collider.center;
    }

    protected override void DoUpdateControls()
    {
        if (_sizeXStorableFloat != null)
            _sizeXStorableFloat.valNoCallback = Collider.size.x;
        if (_sizeYStorableFloat != null)
            _sizeYStorableFloat.valNoCallback = Collider.size.y;
        if (_sizeZStorableFloat != null)
            _sizeZStorableFloat.valNoCallback = Collider.size.z;
        if (_centerXStorableFloat != null)
            _centerXStorableFloat.valNoCallback = Collider.center.x;
        if (_centerYStorableFloat != null)
            _centerYStorableFloat.valNoCallback = Collider.center.y;
        if (_centerZStorableFloat != null)
            _centerZStorableFloat.valNoCallback = Collider.center.z;
    }

    protected override void DoLoadJson(JSONClass jsonClass)
    {
        var size = Collider.size;
        size.x = jsonClass["sizeX"].AsFloat;
        size.y = jsonClass["sizeY"].AsFloat;
        size.z = jsonClass["sizeZ"].AsFloat;
        Collider.size = size;

        var center = Collider.center;
        center.x = jsonClass["centerX"].AsFloat;
        center.y = jsonClass["centerY"].AsFloat;
        center.z = jsonClass["centerZ"].AsFloat;
        Collider.center = center;
    }

    public override JSONClass DoGetJson()
    {
        var jsonClass = new JSONClass();

        jsonClass["sizeX"].AsFloat = Collider.size.x;
        jsonClass["sizeY"].AsFloat = Collider.size.y;
        jsonClass["sizeZ"].AsFloat = Collider.size.z;

        jsonClass["centerX"].AsFloat = Collider.center.x;
        jsonClass["centerY"].AsFloat = Collider.center.y;
        jsonClass["centerZ"].AsFloat = Collider.center.z;

        return jsonClass;
    }

    protected override void DoResetToInitial()
    {
        Collider.size = InitialSize;
        Collider.center = InitialCenter;
    }

    public override IEnumerable<UIDynamic> DoCreateControls()
    {
        yield return Parent.CreateFloatSlider(_sizeXStorableFloat = new JSONStorableFloat("sizeX", Collider.size.x, value =>
        {
            var size = Collider.size;
            size.x = value;
            Collider.size = size;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialSize.x), "Size.X");

        yield return Parent.CreateFloatSlider(_sizeYStorableFloat = new JSONStorableFloat("sizeY", Collider.size.y, value =>
        {
            var size = Collider.size;
            size.y = value;
            Collider.size = size;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialSize.y), "Size.Y");

        yield return Parent.CreateFloatSlider(_sizeZStorableFloat = new JSONStorableFloat("sizeZ", Collider.size.z, value =>
        {
            var size = Collider.size;
            size.z = value;
            Collider.size = size;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialSize.z), "Size.Z");

        yield return Parent.CreateFloatSlider(_centerXStorableFloat = new JSONStorableFloat("centerX", Collider.center.x, value =>
        {
            var center = Collider.center;
            center.x = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.x), "Center.X");

        yield return Parent.CreateFloatSlider(_centerYStorableFloat = new JSONStorableFloat("centerY", Collider.center.y, value =>
        {
            var center = Collider.center;
            center.y = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.y), "Center.Y");

        yield return Parent.CreateFloatSlider(_centerZStorableFloat = new JSONStorableFloat("centerZ", Collider.center.z, value =>
        {
            var center = Collider.center;
            center.z = value;
            Collider.center = center;
            DoUpdatePreview();
        }, -0.25f, 0.25f, false).WithDefault(InitialCenter.z), "Center.Z");
    }

    protected override bool DeviatesFromInitial() => InitialSize != Collider.size || InitialCenter != Collider.center; // Vector3 has built in epsilon equality checks
}

public static class JSONStorableFloatExtensions
{
    public static JSONStorableFloat WithDefault(this JSONStorableFloat jsf, float defaultVal)
    {
        jsf.defaultVal = defaultVal;
        return jsf;
    }
}

public static class ComponentExtensions
{
    public static string Uuid(this Component component)
    {
        var siblings = component.GetComponents<Component>().ToList();
        int siblingIndex = siblings.IndexOf(component);

        var paths = new Stack<string>(new[] { $"{component.name}[{siblingIndex}]" });
        var current = component.gameObject.transform;

        while (current != null && !current.name.Equals("geometry", StringComparison.InvariantCultureIgnoreCase)
                               && !current.name.Equals("Genesis2Female", StringComparison.InvariantCultureIgnoreCase)
                               && !current.name.Equals("Genesis2Male", StringComparison.InvariantCultureIgnoreCase))
        {
            paths.Push($"{current.name}[{current.GetSiblingIndex()}]");
            current = current.transform.parent;
        }

        return string.Join(".", paths.ToArray());
    }

    public static UIDynamic CreateFloatSlider(this MVRScript script, JSONStorableFloat jsf, string label, bool rightSide = true, string valueFormat = "F8")
    {
        var control = script.CreateSlider(jsf, rightSide);
        control.valueFormat = valueFormat;
        control.label = label;
        return control;
    }
}
