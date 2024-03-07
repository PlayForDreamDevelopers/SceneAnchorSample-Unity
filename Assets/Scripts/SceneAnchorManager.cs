using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YVR.Core;

public class SceneAnchorManager : MonoBehaviour
{
    public GameObject anchorPrefab;
    public GameObject quadPrefab;
    public GameObject boxPrefab;
    public GameObject meshPrefab;
    public Text QueryRoomLayoutText;
    private YVRSpatialAnchorResult m_RoomLayoutAnchor;
    private List<YVRSpatialAnchorResult> m_RoomContainerAnchors;
    private List<GameObject> gameObjectsList = new List<GameObject>();
    private Dictionary<YVRSpatialAnchorResult, GameObject> anchorDir = new Dictionary<YVRSpatialAnchorResult, GameObject>();

    private void Start()
    {
        YVRPlugin.Instance.SetPassthrough(true);
    }

    public void PermissionRequest()
    {
        const string spatialPermission = "com.yvr.permission.USE_SCENE";
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(spatialPermission))
        {
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionDenied += Denied;
            callbacks.PermissionGranted += Granted;
            UnityEngine.Android.Permission.RequestUserPermission(spatialPermission, callbacks);
        }
    }
    private void Denied(string permission)  => Debug.Log($"{permission} Denied");
    private void Granted(string permission) => Debug.Log($"{permission} Granted");

    public void OpenSceneCapture()
    {
        YVRSceneAnchor.instance.RequestSceneCapture("", OnSceneCaptureCallback);
    }

    private void OnSceneCaptureCallback(bool result)
    {
        Debug.Log($"Open scene capture result:{result}");
    }

    public void LoadSceneAnchor()
    {
        YVRSpatialAnchorQueryInfo queryInfo = new YVRSpatialAnchorQueryInfo();
        queryInfo.component = YVRSpatialAnchorComponentType.RoomLayout;
        queryInfo.storageLocation = YVRSpatialAnchorStorageLocation.Local;
        YVRSpatialAnchor.instance.QuerySpatialAnchor(queryInfo, OnQueryRoomLayoutAnchor);
    }

    private void OnQueryRoomLayoutAnchor(List<YVRSpatialAnchorResult> queryResult)
    {
        if (queryResult != null && queryResult.Count >= 0)
        {
            QueryRoomLayoutText.text = "";
            m_RoomLayoutAnchor = queryResult.First();
            GetSpaceContainerAnchor();
        }
        else
        {
            QueryRoomLayoutText.text = $"No scene anchor was found";
        }
    }

    public void ResetScene()
    {
        anchorDir.Clear();
        for (int i = gameObjectsList.Count-1; i >= 0; i--)
        {
            Destroy(gameObjectsList[i]);

        }

        gameObjectsList.Clear();
    }

    private void GetSpaceContainerAnchor()
    {
        YVRSceneAnchor.instance.GetAnchorContainer(m_RoomLayoutAnchor.anchorHandle, out List<YVRSpatialAnchorUUID> containerReshlut);
        YVRSpatialAnchorQueryInfo queryInfo = new YVRSpatialAnchorQueryInfo();
        queryInfo.storageLocation = YVRSpatialAnchorStorageLocation.Local;
        queryInfo.ids = containerReshlut.ToArray();
        YVRSpatialAnchor.instance.QuerySpatialAnchor(queryInfo, OnQuerySpatialContainerCallback);
    }

    private void OnQuerySpatialContainerCallback(List<YVRSpatialAnchorResult> queryResult)
    {
        m_RoomContainerAnchors = queryResult;
        LoadSceneGameObject();
        Debug.LogError("LoadSceneGameObject done");
    }

    private void LoadSceneGameObject()
    {
        foreach (var item in m_RoomContainerAnchors)
        {
            LoadBox2DGameObject(item);
            LoadBox3DGameObject(item);
        }
    }

    private void LoadBox2DGameObject(YVRSpatialAnchorResult anchorResult)
    {
        YVRSpatialAnchor.instance.GetSpatialAnchorComponentStatus(anchorResult.anchorHandle, YVRSpatialAnchorComponentType.Bounded2D, out YVRSpatialAnchorComponentStatus status);
        if (status.enable)
        {
            YVRSpatialAnchor.instance.GetSpatialAnchorComponentStatus(anchorResult.anchorHandle, YVRSpatialAnchorComponentType.SemanticLabels, out YVRSpatialAnchorComponentStatus labelsStatus);
            if (labelsStatus.enable)
            {
                YVRSceneAnchor.instance.GetAnchorSemanticLabels(anchorResult.anchorHandle, out string labels);
                if (labels.Contains(YVRSemanticClassification.k_Floor) || labels.Contains(YVRSemanticClassification.K_Ceiling))
                {
                    Debug.Log($"GetAnchorSemanticLabels:{labels}");
                    LoadBoundary2dGameObject(anchorResult);
                    return;
                }
            }
            YVRSceneAnchor.instance.GetAnchorBoundingBox2D(anchorResult.anchorHandle, out YVRRect2D boundingBox2D);
            GameObject anchor = CreateAnchor(anchorResult);
            GameObject go = Instantiate(quadPrefab);
            go.transform.SetParent(anchor.transform);
            go.transform.localPosition = new Vector3(boundingBox2D.offset.x, boundingBox2D.offset.y, 0);
            go.transform.localScale = new Vector3(boundingBox2D.extent.x, boundingBox2D.extent.y, 1);
            go.transform.localEulerAngles = Vector3.zero;
            go.gameObject.SetActive(true);
            gameObjectsList.Add(go);
            LoadSemanticLabels(anchorResult, go);
        }
    }

    private GameObject CreateAnchor(YVRSpatialAnchorResult anchorResult)
    {
        GameObject anchor = Instantiate(anchorPrefab);
        YVRSpatialAnchor.instance.GetSpatialAnchorPose(anchorResult.anchorHandle, out Vector3 position, out Quaternion rotation, out YVRAnchorLocationFlags locationFlags);
        anchor.transform.position = position;
        anchor.transform.rotation = rotation;
        anchorDir.Add(anchorResult, anchor);
        return anchor;
    }

    private void LoadBoundary2dGameObject(YVRSpatialAnchorResult anchorResult)
    {
        YVRSceneAnchor.instance.GetAnchorBoundary2D(anchorResult.anchorHandle, out List<Vector2> boundary2D);
        GameObject anchor = CreateAnchor(anchorResult);
        GameObject go = Instantiate(meshPrefab);
        go.transform.SetParent(anchor.transform);
        go.GetComponent<MeshFilter>().mesh = CreateMesh(boundary2D);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        Debug.Log($"anchorResult:{anchorResult}anchor.transform.position:{anchor.transform.position},go.position:{go.transform.position}");
        go.SetActive(true);
        gameObjectsList.Add(go);
    }

    private Mesh CreateMesh(List<Vector2> boundary2D)
    {
        Mesh mesh = new Mesh();
        mesh.vertices = boundary2D.Select(item => new Vector3(item.x, item.y, 0)).ToArray();
        List<int>indices = new List<int>();
        GenerateIndices(indices, mesh.vertices);
        mesh.triangles = indices.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void GenerateIndices(List<int> indices, Vector3[] vertices)
    {
        var numBoundaryVertices = vertices.Length - 1;
        var centerIndex = numBoundaryVertices;

        for (int i = 0; i < numBoundaryVertices; ++i)
        {
            int j = (i + 1) % numBoundaryVertices;
            indices.Add(centerIndex);
            indices.Add(i);
            indices.Add(j);
            indices.Add(centerIndex);
            indices.Add(j);
            indices.Add(i);
        }
    }

    private void LoadBox3DGameObject(YVRSpatialAnchorResult anchorResult)
    {
        YVRSpatialAnchor.instance.GetSpatialAnchorComponentStatus(anchorResult.anchorHandle, YVRSpatialAnchorComponentType.Bounded3D, out YVRSpatialAnchorComponentStatus status);
        if (status.enable)
        {
            YVRSceneAnchor.instance.GetAnchorBoundingBox3D(anchorResult.anchorHandle, out YVRRect3D boundingBox3D);
            GameObject anchor = CreateAnchor(anchorResult);
            GameObject go = Instantiate(boxPrefab);
            go.transform.SetParent(anchor.transform);
            go.transform.localScale = new Vector3(boundingBox3D.extent.x, boundingBox3D.extent.y, boundingBox3D.extent.z);
            go.transform.localEulerAngles = Vector3.zero;
            go.transform.localPosition = new Vector3(boundingBox3D.offset.x, boundingBox3D.offset.y, boundingBox3D.offset.z);
            go.gameObject.SetActive(true);
            gameObjectsList.Add(anchor);
            LoadSemanticLabels(anchorResult, go);
        }
    }

    private void LoadSemanticLabels(YVRSpatialAnchorResult anchorResult, GameObject go)
    {
        YVRSpatialAnchor.instance.GetSpatialAnchorComponentStatus(anchorResult.anchorHandle, YVRSpatialAnchorComponentType.SemanticLabels, out YVRSpatialAnchorComponentStatus status);
        if (status.enable)
        {
            YVRSceneAnchor.instance.GetAnchorSemanticLabels(anchorResult.anchorHandle, out string labels);
            Debug.Log($"uuid: {new string(anchorResult.uuid)}, labels:{labels}");
            go.GetComponentInChildren<Text>().text = labels;
            AnchorItem anchorItem = go.AddComponent<AnchorItem>();
            anchorItem.SetAnchorItem(anchorResult);
        }
    }
}
