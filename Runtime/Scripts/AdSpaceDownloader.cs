using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

public class AdSpaceDownloader : MonoBehaviour
{
	// INSPECTOR DATA
	public string token;

	// URL
    [HideInInspector]
	private string baseUrl = "http://127.0.0.1:8000";

    [HideInInspector]
	private string jsonUrl;

    [HideInInspector]
	private string downloadObjectUrl;

	// OBJECT
    [HideInInspector]
	private AdData adData = new AdData();

    private void Awake()
    {
    	downloadObjectUrl = baseUrl + "/storage/resources/objects/";

        jsonUrl = baseUrl + "/api/unity/get-ad-data?slug_vsa=" + token;
        StartCoroutine(GetRequest());
    }

    private IEnumerator GetRequest()
    {
    	Debug.Log("--- GET REQUEST ---");
        UnityWebRequest request = UnityWebRequest.Get(jsonUrl);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
        	// Get response text
            string responseText = request.downloadHandler.text;

            // Transform text into C# object
            Response dataObject = JsonUtility.FromJson<Response>(responseText);

            StartCoroutine(DownloadObject(dataObject.resource));
        }
        else
        {
            Debug.Log("Request error: " + request.error);
        }
    }

    private IEnumerator DownloadObject(Resource resource)
    {
    	Debug.Log("--- DOWNLOAD OBJECT ---");
        string downloadUrl = downloadObjectUrl + resource.resource_object.src_res_obj;

        // Download glTF object
        UnityWebRequest objRequest = UnityWebRequest.Get(downloadUrl);
        yield return objRequest.SendWebRequest();

        // Get download error
        if (objRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error downloading glTf file: " + objRequest.error);
            yield break;
        }

        string relativePath = "Assets/AdsManager/Temp/Resources/Objects"; // Relative route to glTF object inside project
        string objFilePath = relativePath + "/" + resource.resource_object.src_res_obj;
        
        adData.resource.orientation = new Vector3(resource.resource_object.resource_orientation.x_degrees_ro, resource.resource_object.resource_orientation.y_degrees_ro, resource.resource_object.resource_orientation.z_degrees_ro);

        System.IO.File.WriteAllBytes(objFilePath, objRequest.downloadHandler.data);
        yield return null;

        // Import assets into Unity
        AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.Default);
        yield return null;

        AssetDatabase.Refresh();
        yield return null;

        StartCoroutine(InstantiateObject(objFilePath));
    }

    private IEnumerator InstantiateObject(string objFilePath)
    {
    	Debug.Log("--- INSTANTIATE OBJECT ---");
        // Get Parent GameObject
        adData.parent.gameObject = gameObject;

        // Get Child Object Prefab
        GameObject objPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(objFilePath);
        adData.child.gameObject = GameObject.Instantiate(objPrefab);
        adData.child.gameObject.transform.localPosition = adData.parent.gameObject.transform.localPosition;

        // SAVE DATA
        StartCoroutine(SaveData());
        yield return null;

        // ROTATE
        StartCoroutine(RotateObject());
        yield return null;

        // SCALE
        StartCoroutine(ScaleObject());
        yield return null;

        // CENTER
        StartCoroutine(CenterObject());
        yield return null;

        // SET PARENT
        StartCoroutine(SetParent());
        yield return null;

        // ALIGN TO WALL
        StartCoroutine(AlignToWall());
        yield return null;

        // GET AND CREATE LIGTH POINTS
        // StartCoroutine(GetLightPoints());
        yield return null;
    }

    private IEnumerator SaveData()
    {
        Debug.Log("--- SAVE DATA ---");
        ChangeRotationAxis(adData.resource.orientation.x, adData.resource.orientation.y, adData.resource.orientation.z);
        adData.child.localScale = GenerateVector3(adData.child.gameObject.transform.localScale.x, adData.child.gameObject.transform.localScale.y, adData.child.gameObject.transform.localScale.z);
        adData.child.lossyScale = GenerateVector3(adData.child.gameObject.transform.lossyScale.x, adData.child.gameObject.transform.lossyScale.y, adData.child.gameObject.transform.lossyScale.z);
        adData.child.localRotation = GenerateVector3(adData.child.gameObject.transform.localRotation.eulerAngles.x, adData.child.gameObject.transform.localRotation.eulerAngles.y, adData.child.gameObject.transform.localRotation.eulerAngles.z);
        adData.parent.lossyScale = new Vector3(adData.parent.gameObject.transform.lossyScale.x, adData.parent.gameObject.transform.lossyScale.y, adData.parent.gameObject.transform.lossyScale.z);
        adData.parent.localRotation = new Vector3(adData.parent.gameObject.transform.localRotation.eulerAngles.x, adData.parent.gameObject.transform.localRotation.eulerAngles.y, adData.parent.gameObject.transform.localRotation.eulerAngles.z);

        RenderLimit rl = this.GetCurrentBounds();
        yield return null;
        
        adData.child.renderer.originalSize = new Vector3(rl.renderMaxLimit.x - rl.renderMinLimit.x, rl.renderMaxLimit.y - rl.renderMinLimit.y, rl.renderMaxLimit.z - rl.renderMinLimit.z);
        adData.child.renderer.originalCenter = new Vector3(rl.renderMinLimit.x + ((rl.renderMaxLimit.x - rl.renderMinLimit.x) / 2), rl.renderMinLimit.y + ((rl.renderMaxLimit.y - rl.renderMinLimit.y) / 2), rl.renderMinLimit.z + ((rl.renderMaxLimit.z - rl.renderMinLimit.z) / 2));
        adData.child.renderer.newSize = GenerateVector3(adData.child.renderer.originalSize.x, adData.child.renderer.originalSize.y, adData.child.renderer.originalSize.z);
        adData.child.renderer.newCenter = adData.child.renderer.originalCenter;
        yield return null;
    }

    private IEnumerator RotateObject()
    {
    	Debug.Log("--- ROTATE OBJECT ---");
        adData.child.gameObject.transform.localRotation = Quaternion.identity;
        adData.child.gameObject.transform.localRotation = Quaternion.Euler(adData.child.localRotation + adData.parent.localRotation + adData.resource.orientation);
        yield return null;
    }

    private IEnumerator ScaleObject()
    {
    	Debug.Log("--- SCALE OBJECT ---");
    	Vector3 props = new Vector3(adData.parent.lossyScale.x / adData.child.renderer.newSize.x, adData.parent.lossyScale.y / adData.child.renderer.newSize.y, adData.parent.lossyScale.z / adData.child.renderer.newSize.z);

        Vector3 scaleToApply = new Vector3(0, 0, 0);

        // Get minimum value
        adData.child.renderer.multProp = Mathf.Min(props.x, props.y, props.z);

        if (adData.child.renderer.multProp == props.x || adData.child.renderer.multProp == props.y || adData.child.renderer.multProp == props.z)
        {
            scaleToApply = new Vector3(adData.child.renderer.multProp * adData.child.lossyScale.x, adData.child.renderer.multProp * adData.child.lossyScale.y, adData.child.renderer.multProp * adData.child.lossyScale.z);
        }
        adData.child.renderer.newSize = new Vector3(adData.child.renderer.newSize.x * adData.child.renderer.multProp, adData.child.renderer.newSize.y * adData.child.renderer.multProp, adData.child.renderer.newSize.z * adData.child.renderer.multProp);

        adData.child.gameObject.transform.localScale = scaleToApply;
        yield return null;
    }

    private IEnumerator CenterObject()
    {
        Debug.Log("--- CENTER OBJECT ---");

        RenderLimit rl = this.GetCurrentBounds();
        yield return null;

        adData.child.renderer.newCenter = new Vector3(rl.renderMinLimit.x + ((rl.renderMaxLimit.x - rl.renderMinLimit.x) / 2), rl.renderMinLimit.y + ((rl.renderMaxLimit.y - rl.renderMinLimit.y) / 2), rl.renderMinLimit.z + ((rl.renderMaxLimit.z - rl.renderMinLimit.z) / 2));

        // Get difference position between parent center and real renderer center
        Vector3 differenceCenter = new Vector3(adData.parent.gameObject.transform.position.x - adData.child.renderer.newCenter.x, adData.parent.gameObject.transform.position.y - adData.child.renderer.newCenter.y, adData.parent.gameObject.transform.position.z - adData.child.renderer.newCenter.z);
        adData.child.renderer.newCenter = new Vector3(adData.child.gameObject.transform.position.x + differenceCenter.x, adData.child.gameObject.transform.position.y + differenceCenter.y, adData.child.gameObject.transform.position.z + differenceCenter.z);

        // Apply new center
        adData.child.gameObject.transform.position = adData.child.renderer.newCenter;
        yield return null;
    }

    private IEnumerator SetParent()
    {
        Debug.Log("--- SET PARENT ---");
        adData.child.gameObject.transform.SetParent(adData.parent.gameObject.transform);
        yield return null;
    }

    private IEnumerator AlignToWall()
    {
        Debug.Log("--- ALIGN TO WALL ---");
        // Get distance between parent and renderer
        float desiredZ = (adData.parent.lossyScale.z / 2) - (adData.child.renderer.newSize.z / 2);

        // Get Z axis
        Vector3 localZDirection = adData.parent.gameObject.transform.TransformDirection(Vector3.forward);

        // Apply Z distance with rotation
        adData.child.gameObject.transform.position += new Vector3(localZDirection.x * -desiredZ, localZDirection.y * -desiredZ, localZDirection.z * -desiredZ);

        yield return null;
    }

    private IEnumerator GetLightPoints()
    {
        // Loop all child to get light points
        Transform[] points = LoopChilds(adData.child.gameObject.transform);
        foreach(Transform point in points)
        {
            CreateLight(point);

        }
        yield return null;
    }

    private Transform[] LoopChilds(Transform transformObject)
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in transformObject)
        {
            // Get child names
            if (child.name.StartsWith("Light"))
            {
                points.Add(child);
            }
            
            // If chils has another nested childs, call recursive function
            if (child.childCount > 0)
            {
                LoopChilds(child);
            }
        }
        return points.ToArray();
    }

    private void CreateLight(Transform lightPoint)
    {
        // Create new GameObject
        GameObject lightGameObject = new GameObject("Light");

        // Add Light component to GameObject
        Light lightComponent = lightGameObject.AddComponent<Light>();

        // Set light type (Point)
        lightComponent.type = LightType.Point;

        // Set light color
        lightComponent.color = Color.red;

        // Set light intensity
        lightComponent.intensity = 3.0f;

        // Set light range (distance)
        lightComponent.range = 4.0f;

        // Set light position
        lightGameObject.transform.position = new Vector3(lightPoint.position.x, lightPoint.position.y, lightPoint.position.z);

        // Set light into parent
        lightGameObject.transform.SetParent(adData.child.gameObject.transform);
    }

    private RenderLimit GetCurrentBounds()
    {
        Renderer[] renderers = adData.child.gameObject.GetComponentsInChildren<Renderer>();

        RenderLimit rl = new RenderLimit();
        bool isFirst = true;

        foreach (Renderer r in renderers)
        {
            if (isFirst) {
                isFirst = false;
                rl.renderMinLimit = new Vector3(r.bounds.center.x - r.bounds.extents.x, r.bounds.center.y - r.bounds.extents.y, r.bounds.center.z - r.bounds.extents.z);
                rl.renderMaxLimit = new Vector3(r.bounds.center.x + r.bounds.extents.x, r.bounds.center.y + r.bounds.extents.y, r.bounds.center.z + r.bounds.extents.z);
            } else {
                if ((r.bounds.center.x - r.bounds.extents.x) < rl.renderMinLimit.x)
                {
                    rl.renderMinLimit = new Vector3(r.bounds.center.x - r.bounds.extents.x, rl.renderMinLimit.y, rl.renderMinLimit.z);
                }
                else if ((r.bounds.center.x + r.bounds.extents.x) > rl.renderMaxLimit.x)
                {
                    rl.renderMaxLimit = new Vector3(r.bounds.center.x + r.bounds.extents.x, rl.renderMaxLimit.y, rl.renderMaxLimit.z);
                }
                if ((r.bounds.center.y - r.bounds.extents.y) < rl.renderMinLimit.y)
                {
                    rl.renderMinLimit = new Vector3(rl.renderMinLimit.x, r.bounds.center.y - r.bounds.extents.y, rl.renderMinLimit.z);
                }
                else if ((r.bounds.center.y + r.bounds.extents.y) > rl.renderMaxLimit.y)
                {
                    rl.renderMaxLimit = new Vector3(rl.renderMaxLimit.x, r.bounds.center.y + r.bounds.extents.y, rl.renderMaxLimit.z);
                }
                if ((r.bounds.center.z - r.bounds.extents.z) < rl.renderMinLimit.z)
                {
                    rl.renderMinLimit = new Vector3(rl.renderMinLimit.x, rl.renderMinLimit.y, r.bounds.center.z - r.bounds.extents.z);
                }
                else if ((r.bounds.center.z + r.bounds.extents.z) > rl.renderMaxLimit.z)
                {
                    rl.renderMaxLimit = new Vector3(rl.renderMaxLimit.x, rl.renderMaxLimit.y, r.bounds.center.z + r.bounds.extents.z);
                }
            }
        }
        return rl;
    }

	private Vector3 GenerateVector3(float x, float y, float z)
	{
        Vector3 v = new Vector3(x, y, z);
        switch(adData.child.orientation.x) {
            case 'X':
                v.x = x;
                break;
            case 'Y':
                v.x = y;
                break;
            case 'Z':
                v.x = z;
                break;
        }
        switch(adData.child.orientation.y) {
            case 'X':
                v.y = x;
                break;
            case 'Y':
                v.y = y;
                break;
            case 'Z':
                v.y = z;
                break;
        }
        switch(adData.child.orientation.z) {
            case 'X':
                v.z = x;
                break;
            case 'Y':
                v.z = y;
                break;
            case 'Z':
                v.z = z;
                break;
        }
        return v;
    }

    private void ChangeRotationAxis(float x, float y, float z)
    {
        AxisObject v = new AxisObject('X', 'Y', 'Z');
        if (x == 90 || x == 270) {
            v.y = 'Z';
            v.z = 'Y';
        }

        if (y == 90 || y == 270) {
            v.x = 'Z';
            v.z = 'X';
        }

        if (z == 90 || z == 270) {
            v.x = 'Y';
            v.y = 'X';
        }
        adData.child.orientation = v;
    }

    public class RenderLimit
    {
        public Vector3 renderMinLimit;
        public Vector3 renderMaxLimit;

        public RenderLimit()
        {
            this.renderMinLimit = Vector3.zero;
            this.renderMaxLimit = Vector3.zero;
        }
    }

    // AXIS DATA
    [System.Serializable]
    public class AxisObject
    {
        public char x;
        public char y;
        public char z;
        public AxisObject(char x, char y, char z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    // RESPONSE DATA
    [System.Serializable]
    public class Response
    {
        public int code;
        public string message;
        public Resource resource;
    }

    [System.Serializable]
    public class Resource
    {
        public int id_resource;
        public string name_resource;
        public string slug_resource;
        public string updated_at;
        public ResourceObject resource_object;
    }

    [System.Serializable]
    public class ResourceObject
    {
        public int id_res_obj;
        public string src_res_obj;
        public string original_name_res_obj;
        public int id_resource_res_obj;
        public string updated_at;
        public ResourceOrientation resource_orientation;
    }


    [System.Serializable]
    public class ResourceOrientation
    {
        public int id_ro;
        public int id_res_obj_ro;
        public float x_degrees_ro;
        public float y_degrees_ro;
        public float z_degrees_ro;
        public float x_size_ro;
        public float y_size_ro;
        public float z_size_ro;
    }


    // SAVE DATA
    [System.Serializable]
    public class AdData
    {
    	public AdDataParent parent;
		public AdDataChild child;
		public AdDataResource resource;

		public AdData()
		{
			this.parent = new AdDataParent();
			this.child = new AdDataChild();
			this.resource = new AdDataResource();
		}

		public void toString()
		{
			Debug.Log("--- PARENT ---");
			this.parent.toString();
			Debug.Log("--- CHILD ---");
			this.child.toString();
			Debug.Log("--- RESOURCE ---");
			this.resource.toString();
		}
    }

    [System.Serializable]
    public class AdDataParent
    {
    	public GameObject gameObject;
    	public Vector3 localScale;
    	public Vector3 lossyScale;
    	public Vector3 localRotation;

    	public AdDataParent()
		{
			this.gameObject = null;
			this.localScale = new Vector3(0, 0, 0);
			this.lossyScale = new Vector3(0, 0, 0);
			this.localRotation = new Vector3(0, 0, 0);
		}

		public void toString()
		{
			Debug.Log("Local Scale: (" + this.localScale.x + ", " + this.localScale.y + ", " + this.localScale.z + ")");
			Debug.Log("Lossy Scale: (" + this.lossyScale.x + ", " + this.lossyScale.y + ", " + this.lossyScale.z + ")");
			Debug.Log("Local Rotation: (" + this.localRotation.x + ", " + this.localRotation.y + ", " + this.localRotation.z + ")");
		}
    }

    [System.Serializable]
    public class AdDataChild
    {
    	public GameObject gameObject;
    	public AxisObject orientation;
    	public Vector3 localScale;
    	public Vector3 lossyScale;
    	public Vector3 localRotation;
    	public AdDataRenderer renderer;

		public AdDataChild()
		{
			this.gameObject = null;
			this.orientation = null;
			this.localScale = new Vector3(0, 0, 0);
			this.lossyScale = new Vector3(0, 0, 0);
			this.localRotation = new Vector3(0, 0, 0);
			this.renderer = new AdDataRenderer();
		}

		public void toString()
		{
			Debug.Log("Orientation: (" + this.orientation.x + ", " + this.orientation.y + ", " + this.orientation.z + ")");
			Debug.Log("Local Scale: (" + this.localScale.x + ", " + this.localScale.y + ", " + this.localScale.z + ")");
			Debug.Log("Lossy Scale: (" + this.lossyScale.x + ", " + this.lossyScale.y + ", " + this.lossyScale.z + ")");
			Debug.Log("Local Rotation: (" + this.localRotation.x + ", " + this.localRotation.y + ", " + this.localRotation.z + ")");
			this.renderer.toString();
		}
    }

    [System.Serializable]
    public class AdDataRenderer
    {
    	public Renderer renderer;
    	public Bounds originalBounds;
    	public Vector3 originalSize;
    	public Vector3 originalCenter;
    	public Bounds newBounds;
    	public Vector3 newSize;
    	public Vector3 newCenter;
    	public float multProp;

    	public AdDataRenderer()
		{
			this.renderer = null;
			this.originalBounds = default(Bounds);
			this.originalSize = new Vector3(0, 0, 0);
			this.originalCenter = new Vector3(0, 0, 0);
			this.newBounds = default(Bounds);
			this.newSize = new Vector3(0, 0, 0);
			this.newCenter = new Vector3(0, 0, 0);
			this.multProp = 1;
		}

		public void toString()
		{
			Debug.Log("--- Start renderer ---");
			Debug.Log("Original Size: (" + this.originalSize.x + ", " + this.originalSize.y + ", " + this.originalSize.z + ")");
			Debug.Log("Original Center: (" + this.originalCenter.x + ", " + this.originalCenter.y + ", " + this.originalCenter.z + ")");
			Debug.Log("New Size: (" + this.newSize.x + ", " + this.newSize.y + ", " + this.newSize.z + ")");
			Debug.Log("New Center: (" + this.newCenter.x + ", " + this.newCenter.y + ", " + this.newCenter.z + ")");
			Debug.Log("Multiplication proportion: " + this.multProp);
			Debug.Log("--- End renderer ---");
		}
    }

    [System.Serializable]
    public class AdDataResource
    {
    	public Vector3 orientation;

    	public AdDataResource()
		{
			this.orientation = new Vector3(0, 0, 0);
		}

		public void toString()
		{
			Debug.Log("Orientation: (" + this.orientation.x + ", " + this.orientation.y + ", " + this.orientation.z + ")");
		}
    }
}
