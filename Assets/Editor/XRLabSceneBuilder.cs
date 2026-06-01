using System.IO;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class XRLabSceneBuilder
{
    const string ScenePath = "Assets/Scenes/XRLabScene.unity";

    [MenuItem("Tools/XR Lab/Build Assignment Scene")]
    public static void BuildAssignmentScene()
    {
        ConfigureOpenXR();
        BuildScene();
        Debug.Log("XR lab assignment scene generated.");
    }

    static void ConfigureOpenXR()
    {
        var group = BuildTargetGroup.Standalone;
        var getOrCreate = typeof(XRGeneralSettingsPerBuildTarget).GetMethod(
            "GetOrCreate",
            BindingFlags.Static | BindingFlags.NonPublic);
        var store = (XRGeneralSettingsPerBuildTarget)getOrCreate.Invoke(null, null);

        if (!store.HasSettingsForBuildTarget(group))
            store.CreateDefaultSettingsForBuildTarget(group);

        if (!store.HasManagerSettingsForBuildTarget(group))
            store.CreateDefaultManagerSettingsForBuildTarget(group);

        var manager = store.ManagerSettingsForBuildTarget(group);
        XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);

        EnableOpenXRFeature(group, "com.unity.openxr.feature.input.khrsimpleprofile");
        EnableOpenXRFeature(group, "com.unity.openxr.feature.input.oculustouch");

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssets();
    }

    static void EnableOpenXRFeature(BuildTargetGroup group, string featureId)
    {
        var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
        if (feature != null)
        {
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "XRLabScene";

        Directory.CreateDirectory("Assets/Materials");
        Directory.CreateDirectory("Assets/Input");
        Directory.CreateDirectory("Assets/Scenes");

        var groundMat = MakeMat("Lab_Ground", new Color(0.35f, 0.38f, 0.36f));
        var cubeMat = MakeMat("Lab_Cube_Blue", new Color(0.1f, 0.45f, 1f));
        var sphereMat = MakeMat("Lab_Sphere_Green", new Color(0.25f, 0.8f, 0.35f));
        var tableMat = MakeMat("Lab_Table_Wood", new Color(0.55f, 0.34f, 0.18f));
        var redMat = MakeMat("Left_Controller_Red", new Color(0.95f, 0.2f, 0.2f));
        var tealMat = MakeMat("Right_Controller_Teal", new Color(0.1f, 0.85f, 0.9f));

        var taskRoot = new GameObject("Lab Tasks Evidence Root");
        AddLighting();

        var xrOrigin = InstantiateXRDebugRig(taskRoot.transform, redMat, tealMat);
        InstantiateSimulator();

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground Plane";
        ground.transform.SetParent(taskRoot.transform);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var cube = MakeGrabbablePrimitive(
            "Grabbable Color Cube",
            PrimitiveType.Cube,
            new Vector3(-0.75f, 0.75f, 1.4f),
            Vector3.one * 0.45f,
            cubeMat,
            taskRoot.transform);
        MakeGrabbablePrimitive(
            "Grabbable Sphere",
            PrimitiveType.Sphere,
            new Vector3(0.05f, 0.7f, 1.35f),
            Vector3.one * 0.42f,
            sphereMat,
            taskRoot.transform);
        MakeGrabbableTable(tableMat, taskRoot.transform);

        var primaryRef = CreateInputActionAssets();
        var driver = new GameObject("Input Action Demo - Primary Button Changes Cube Color");
        driver.transform.SetParent(taskRoot.transform);
        var changer = driver.AddComponent<PrimaryButtonColorChanger>();
        var serializedChanger = new SerializedObject(changer);
        serializedChanger.FindProperty("primaryButtonAction").objectReferenceValue = primaryRef;
        serializedChanger.FindProperty("targetRenderer").objectReferenceValue = cube.GetComponent<Renderer>();
        serializedChanger.ApplyModifiedPropertiesWithoutUndo();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static Material MakeMat(string name, Color color)
    {
        var path = $"Assets/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AddLighting()
    {
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static GameObject InstantiateXRDebugRig(Transform taskRoot, Material leftMat, Material rightMat)
    {
        const string prefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var xrOrigin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        xrOrigin.name = "XR Origin (XR Rig) - Floor Tracking";
        xrOrigin.transform.SetParent(taskRoot);
        xrOrigin.transform.position = Vector3.zero;

        var origin = xrOrigin.GetComponent<XROrigin>();
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        origin.CameraYOffset = 1.7f;
        if (origin.Camera != null)
        {
            origin.Camera.tag = "MainCamera";
            origin.Camera.transform.localPosition = new Vector3(0f, 1.7f, -1.2f);
            origin.Camera.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            origin.Camera.nearClipPlane = 0.05f;
        }

        AddControllerModel(xrOrigin, "Left Controller", leftMat);
        AddControllerModel(xrOrigin, "Right Controller", rightMat);
        return xrOrigin;
    }

    static void InstantiateSimulator()
    {
        const string prefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.0/XR Interaction Simulator/XR Interaction Simulator.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return;

        var simulator = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        simulator.name = "XR Interaction Simulator";
    }

    static void AddControllerModel(GameObject xrOrigin, string controllerName, Material material)
    {
        var parent = xrOrigin.transform.Find("Camera Offset/" + controllerName) ??
            xrOrigin.transform.Find(controllerName);
        if (parent == null)
            return;

        var modelRoot = new GameObject(controllerName + " Model");
        modelRoot.transform.SetParent(parent, false);
        modelRoot.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Controller Body";
        body.transform.SetParent(modelRoot.transform, false);
        body.transform.localScale = new Vector3(0.05f, 0.12f, 0.05f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Interaction Tip";
        tip.transform.SetParent(modelRoot.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.13f);
        tip.transform.localScale = Vector3.one * 0.04f;
        tip.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(tip.GetComponent<Collider>());
    }

    static GameObject MakeGrabbablePrimitive(
        string name,
        PrimitiveType type,
        Vector3 position,
        Vector3 scale,
        Material material,
        Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        AddGrabBehavior(go, 0.5f);
        return go;
    }

    static void MakeGrabbableTable(Material material, Transform parent)
    {
        var table = new GameObject("Grabbable Table");
        table.transform.SetParent(parent);
        table.transform.position = new Vector3(0.95f, 0.55f, 1.55f);
        AddGrabBehavior(table, 2f);

        AddTablePart(table.transform, "Table Top", new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 0.12f, 0.7f), material);
        AddTablePart(table.transform, "Front Left Leg", new Vector3(-0.38f, -0.05f, 0.24f), new Vector3(0.1f, 0.7f, 0.1f), material);
        AddTablePart(table.transform, "Front Right Leg", new Vector3(0.38f, -0.05f, 0.24f), new Vector3(0.1f, 0.7f, 0.1f), material);
        AddTablePart(table.transform, "Back Left Leg", new Vector3(-0.38f, -0.05f, -0.24f), new Vector3(0.1f, 0.7f, 0.1f), material);
        AddTablePart(table.transform, "Back Right Leg", new Vector3(0.38f, -0.05f, -0.24f), new Vector3(0.1f, 0.7f, 0.1f), material);
    }

    static void AddTablePart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void AddGrabBehavior(GameObject go, float mass)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        go.AddComponent<ReachHighlight>();
    }

    static InputActionReference CreateInputActionAssets()
    {
        const string inputAssetPath = "Assets/Input/XRLabControls.inputactions";
        const string inputReferencePath = "Assets/Input/PrimaryButtonChangeColor.asset";

        DeleteAssetIfExists(inputAssetPath);
        DeleteAssetIfExists(inputReferencePath);

        var inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
        inputAsset.name = "XRLabControls";
        var map = new InputActionMap("XR Lab Controls");
        inputAsset.AddActionMap(map);

        AddAction(map, "Left Trigger", InputActionType.Value, "Axis", "<XRController>{LeftHand}/trigger");
        AddAction(map, "Right Trigger", InputActionType.Value, "Axis", "<XRController>{RightHand}/trigger");
        AddAction(map, "Left Grip", InputActionType.Value, "Axis", "<XRController>{LeftHand}/grip");
        AddAction(map, "Right Grip", InputActionType.Value, "Axis", "<XRController>{RightHand}/grip");
        var primary = AddAction(
            map,
            "Primary Button - Change Color",
            InputActionType.Button,
            "Button",
            "<XRController>{LeftHand}/primaryButton",
            "<XRController>{RightHand}/primaryButton",
            "<Keyboard>/c");
        AddAction(map, "Secondary Button", InputActionType.Button, "Button", "<XRController>{LeftHand}/secondaryButton", "<XRController>{RightHand}/secondaryButton");
        AddAction(map, "Left Thumbstick", InputActionType.Value, "Vector2", "<XRController>{LeftHand}/thumbstick");
        AddAction(map, "Right Thumbstick", InputActionType.Value, "Vector2", "<XRController>{RightHand}/thumbstick");

        File.WriteAllText(inputAssetPath, inputAsset.ToJson());
        Object.DestroyImmediate(inputAsset);
        AssetDatabase.ImportAsset(inputAssetPath, ImportAssetOptions.ForceUpdate);

        var importedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputAssetPath);
        var importedPrimary = importedAsset.FindAction(primary.name);
        var primaryRef = InputActionReference.Create(importedPrimary);
        primaryRef.name = "Primary Button - Change Color Reference";
        AssetDatabase.CreateAsset(primaryRef, inputReferencePath);
        return primaryRef;
    }

    static InputAction AddAction(InputActionMap map, string name, InputActionType type, string expectedControlType, params string[] bindings)
    {
        var action = map.AddAction(name, type: type, expectedControlLayout: expectedControlType);
        foreach (var binding in bindings)
            action.AddBinding(binding);
        return action;
    }

    static void DeleteAssetIfExists(string path)
    {
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            AssetDatabase.DeleteAsset(path);
        else if (File.Exists(path))
            File.Delete(path);
    }
}
