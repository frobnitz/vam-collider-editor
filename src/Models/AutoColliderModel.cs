using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

public class AutoColliderModel : ColliderContainerModelBase<AutoCollider>, IModel
{
    private readonly bool _initialCollisionEnabled;
    private readonly float _initialColliderLength;
    private readonly float _initialColliderRadius;
    private readonly float _initialHardColliderBuffer;
    private readonly float _initialColliderLookOffset;
    private readonly float _initialColliderUpOffset;
    private readonly float _initialColliderRightOffset;
    private readonly float _initialAutoLengthBuffer;
    private readonly float _initialAutoRadiusBuffer;
    private readonly float _initialAutoRadiusMultiplier;
    private readonly List<ColliderModel> _ownedColliders = new List<ColliderModel>();
    private float _lastAutoRadiusMultiplier;

    protected override bool OwnsColliders => true;

    public string Type => "Auto Collider";
    public AutoCollider AutoCollider => Component;
    public AutoColliderGroupModel AutoColliderGroup { get; set; }

    public AutoColliderModel(MVRScript script, AutoCollider autoCollider, ColliderPreviewConfig config)
        : base(script, autoCollider, $"[au] {NameHelper.Simplify(autoCollider.name)}")
    {
        _initialCollisionEnabled = autoCollider.collisionEnabled;
        _initialColliderLength = autoCollider.colliderLength;
        _initialColliderRadius = autoCollider.colliderRadius;
        _initialHardColliderBuffer = autoCollider.hardColliderBuffer;
        _initialColliderLookOffset = autoCollider.colliderLookOffset;
        _initialColliderUpOffset = autoCollider.colliderUpOffset;
        _initialColliderRightOffset = autoCollider.colliderRightOffset;
        _initialAutoLengthBuffer = autoCollider.autoLengthBuffer;
        _initialAutoRadiusBuffer = autoCollider.autoRadiusBuffer;
        _initialAutoRadiusMultiplier = _lastAutoRadiusMultiplier = autoCollider.autoRadiusMultiplier;
        if (Component.hardCollider != null) _ownedColliders.Add(ColliderModel.CreateTyped(script, autoCollider.hardCollider, config));
        if (Component.jointCollider != null) _ownedColliders.Add(ColliderModel.CreateTyped(script, Component.jointCollider, config));
    }

    protected override void CreateControlsInternal()
    {
        if (AutoColliderGroup != null)
        {
            var goToAutoColliderGroupButton = Script.CreateButton($"Go to autocollider group {AutoColliderGroup.Label}", true);
            goToAutoColliderGroupButton.button.onClick.AddListener(() =>
            {
                Script.SendMessage("SelectEditable", AutoColliderGroup);
            });
            RegisterControl(goToAutoColliderGroupButton);
        }

        RegisterControl(
                Script.CreateToggle(RegisterStorable(
                    new JSONStorableBool("collisionEnabled", Component.collisionEnabled, value =>
                    {
                        Component.collisionEnabled = value;
                        RefreshAutoCollider();
                        SetModified();
                    })
                    .WithDefault(_initialCollisionEnabled)
                ), "Collision Enabled")
        );

        if (Component.useAutoLength)
        {
            RegisterControl(
                    Script.CreateFloatSlider(RegisterStorable(
                        new JSONStorableFloat("autoLengthBuffer", Component.autoLengthBuffer, value =>
                        {
                            Component.autoLengthBuffer = value;
                            RefreshAutoCollider();
                            SetModified();
                        }, -0.25f, 0.25f, false)
                        .WithDefault(_initialAutoLengthBuffer)
                    ), "Auto Length Buffer")
            );
        }
        else
        {
            RegisterControl(
                    Script.CreateFloatSlider(RegisterStorable(
                        new JSONStorableFloat("colliderLength", Component.colliderLength, value =>
                        {
                            Component.colliderLength = value;
                            RefreshAutoCollider();
                            SetModified();
                        }, 0f, 0.25f, false)
                        .WithDefault(_initialColliderLength)
                    ), "Length")
            );
        }

        if (Component.useAutoRadius)
        {
            RegisterControl(
                    Script.CreateFloatSlider(RegisterStorable(
                        new JSONStorableFloat("autoRadiusBuffer", Component.autoRadiusBuffer, value =>
                        {
                            Component.autoRadiusBuffer = value;
                            RefreshAutoCollider();
                            SetModified();
                        }, -0.025f, 0.025f, false)
                        .WithDefault(_initialAutoRadiusBuffer)
                    ), "Auto Radius Buffer")
            );

            RegisterControl(
                    Script.CreateFloatSlider(RegisterStorable(
                        new JSONStorableFloat("autoRadiusMultiplier", Component.autoRadiusMultiplier, value =>
                        {
                            Component.autoRadiusMultiplier = _lastAutoRadiusMultiplier = value;
                            RefreshAutoCollider();
                            SetModified();
                        }, 0.001f, 2f, false)
                        .WithDefault(_initialAutoRadiusMultiplier)
                    ), "Auto Radius Multiplier")
            );
        }
        else
        {
            RegisterControl(
                    Script.CreateFloatSlider(RegisterStorable(
                        new JSONStorableFloat("colliderRadius", Component.colliderRadius, value =>
                        {
                            Component.colliderRadius = value;
                            RefreshAutoCollider();
                            SetModified();
                        }, 0f, 0.25f, false)
                        .WithDefault(_initialColliderRadius)
                    ), "Radius")
            );

            RegisterControl(
                    Script.CreateFloatSlider(RegisterStorable(
                        new JSONStorableFloat("hardColliderBuffer", Component.hardColliderBuffer, value =>
                        {
                            Component.hardColliderBuffer = value;
                            RefreshAutoCollider();
                            SetModified();
                        }, 0f, 0.25f, false)
                        .WithDefault(_initialHardColliderBuffer)
                    ), "Hard Collider Buffer")
            );
        }

        RegisterControl(
                Script.CreateFloatSlider(RegisterStorable(
                    new JSONStorableFloat("colliderLookOffset", Component.colliderLookOffset, value =>
                    {
                        Component.colliderLookOffset = value;
                        RefreshAutoCollider();
                        SetModified();
                    }, -0.25f, 0.25f, false)
                    .WithDefault(_initialColliderLookOffset)
                ), "Look Offset")
        );

        RegisterControl(
                Script.CreateFloatSlider(RegisterStorable(
                    new JSONStorableFloat("colliderUpOffset", Component.colliderUpOffset, value =>
                    {
                        Component.colliderUpOffset = value;
                        RefreshAutoCollider();
                        SetModified();
                    }, -0.25f, 0.25f, false)
                    .WithDefault(_initialColliderUpOffset)
                ), "Up Offset")
        );

        RegisterControl(
                Script.CreateFloatSlider(RegisterStorable(
                    new JSONStorableFloat("colliderRightOffset", Component.colliderRightOffset, value =>
                    {
                        Component.colliderRightOffset = value;
                        RefreshAutoCollider();
                        SetModified();
                    }, -0.25f, 0.25f, false)
                    .WithDefault(_initialColliderRightOffset)
                ), "Right Offset")
        );
    }

    public void RefreshAutoCollider()
    {
        var previousResizeTrigger = AutoCollider.resizeTrigger;
        AutoCollider.resizeTrigger = AutoCollider.ResizeTrigger.Always;
        AutoCollider.AutoColliderSizeSet(true);
        AutoCollider.resizeTrigger = previousResizeTrigger;
        SyncPreview();
    }

    public void SyncToCollider()
    {
        if (Modified)
        {
            if (Component.autoRadiusMultiplier != _lastAutoRadiusMultiplier)
                Component.autoRadiusMultiplier = _lastAutoRadiusMultiplier;
            return;
        }
    }

    protected override void SetSelected(bool value)
    {
        // TODO: Track colliders to highlight them
        base.SetSelected(value);
    }

    protected override void DoLoadJson(JSONClass jsonClass)
    {
        LoadJsonField(jsonClass, "collisionEnabled", val => Component.collisionEnabled = val);

        if (Component.useAutoLength)
        {
            LoadJsonField(jsonClass, "autoLengthBuffer", val => Component.autoLengthBuffer = val);
        }
        else
        {
            LoadJsonField(jsonClass, "colliderLength", val => Component.colliderLength = val);
        }
        if (Component.useAutoRadius)
        {
            LoadJsonField(jsonClass, "autoRadiusBuffer", val => Component.autoRadiusBuffer = val);
            LoadJsonField(jsonClass, "autoRadiusMultiplier", val => Component.autoRadiusMultiplier = _lastAutoRadiusMultiplier = val);
        }
        else
        {
            LoadJsonField(jsonClass, "colliderRadius", val => Component.colliderRadius = val);
            LoadJsonField(jsonClass, "hardColliderBuffer", val => Component.hardColliderBuffer = val);
        }

        LoadJsonField(jsonClass, "colliderLookOffset", val => Component.colliderLookOffset = val);
        LoadJsonField(jsonClass, "colliderUpOffset", val => Component.colliderUpOffset = val);
        LoadJsonField(jsonClass, "colliderRightOffset", val => Component.colliderRightOffset = val);
    }

    protected override JSONClass DoGetJson()
    {
        var jsonClass = new JSONClass();
        jsonClass["collisionEnabled"].AsBool = Component.collisionEnabled;
        if (Component.useAutoLength)
        {
            jsonClass["autoLengthBuffer"].AsFloat = Component.autoLengthBuffer;
        }
        else
        {
            jsonClass["colliderLength"].AsFloat = Component.colliderLength;
        }
        if (Component.useAutoRadius)
        {
            jsonClass["autoRadiusBuffer"].AsFloat = Component.autoRadiusBuffer;
            jsonClass["autoRadiusMultiplier"].AsFloat = Component.autoRadiusMultiplier;
        }
        else
        {
            jsonClass["colliderRadius"].AsFloat = Component.colliderRadius;
            jsonClass["hardColliderBuffer"].AsFloat = Component.hardColliderBuffer;
        }
        jsonClass["colliderLookOffset"].AsFloat = Component.colliderLookOffset;
        jsonClass["colliderUpOffset"].AsFloat = Component.colliderUpOffset;
        jsonClass["colliderRightOffset"].AsFloat = Component.colliderRightOffset;
        return jsonClass;
    }

    protected override void DoResetToInitial()
    {
        Component.collisionEnabled = _initialCollisionEnabled;
        if (Component.useAutoLength)
        {
            Component.autoLengthBuffer = _initialAutoLengthBuffer;
        }
        else
        {
            Component.colliderLength = _initialColliderLength;
        }
        if (Component.useAutoRadius)
        {
            Component.autoRadiusBuffer = _initialAutoRadiusBuffer;
            Component.autoRadiusMultiplier = _lastAutoRadiusMultiplier = _initialAutoRadiusMultiplier;
        }
        else
        {
            Component.colliderRadius = _initialColliderRadius;
            Component.hardColliderBuffer = _initialHardColliderBuffer;
        }
        Component.colliderLookOffset = _initialColliderLookOffset;
        Component.colliderUpOffset = _initialColliderUpOffset;
        Component.colliderRightOffset = _initialColliderRightOffset;

        RefreshAutoCollider();
    }

    public override IEnumerable<ColliderModel> GetColliders() => _ownedColliders;

    public IEnumerable<Rigidbody> GetRigidbodies()
    {
        if (Component.jointRB != null) yield return Component.jointRB;
        if (Component.kinematicRB != null) yield return Component.kinematicRB;
    }
}
