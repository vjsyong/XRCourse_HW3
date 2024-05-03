using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using static UnityEditor.SceneView;

[RequireComponent(typeof(ARRaycastManager))]
[RequireComponent(typeof(ARAnchorManager))]
[RequireComponent(typeof(ARPointCloudManager))]


public class HW3 : MonoBehaviour
{
    [SerializeField]
    string hostIP;
    [SerializeField] 
    int hostPort;

    [SerializeField]
    ARCameraManager cameraManager;

    [SerializeField]
    TMPro.TextMeshProUGUI log;

    [SerializeField]
    Button toggleButton;

    [SerializeField]
    GameObject cubePrefab;


    private TcpClient socketConnection;
    private Thread clientReceiveThread;

    private Camera _camera;

    private ARRaycastManager raycastManager;

    private ARAnchorManager anchorManager;

    private ARPointCloudManager pointCloudManager;

    private List<ARAnchor> anchors = new List<ARAnchor>();

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();


    //Vector2 StringToVector2(string input)
    //{
    //    input = input.Replace("(", "");
    //    input = input.Replace(")", "");
    //    string[] values = input.Split(',');
    //    return new Vector2(float.Parse(values[0]), float.Parse(values[1]));
    //}

    private void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        anchorManager = GetComponent<ARAnchorManager>();
        pointCloudManager = GetComponent<ARPointCloudManager>();

        toggleButton.onClick.AddListener(ToggleDetection);
    }

    private void ToggleDetection()
    {
        pointCloudManager.enabled = !pointCloudManager.enabled;

        foreach(ARPointCloud pointCloud in pointCloudManager.trackables)
        {
            pointCloud.gameObject.SetActive(pointCloudManager.enabled);
        }

        toggleButton.GetComponentInChildren<TextMeshProUGUI>().text = pointCloudManager.enabled ?
            "Disable Plane Detection" : "Enable Plane Detection";

    }

    public void captureCameraImage()
    {

        ConnectToTcpServer();
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        Resolution currentResolution = Screen.currentResolution;

        var savedPos = Camera.main.transform.position;
        var savedRot = Camera.main.transform.rotation;



        // Set up our conversion params
        var conversionParams = new XRCpuImage.ConversionParams
        {
            // Convert the entire image
            inputRect = new RectInt(0, 0, image.width, image.height),

            // Output at full resolution
            outputDimensions = new Vector2Int(image.width, image.height),

            // Convert to RGBA format
            outputFormat = TextureFormat.RGB24,

            // Flip across the vertical axis (mirror image)
            transformation = XRCpuImage.Transformation.MirrorY
        };
        var texture = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
        var rawTextureData = texture.GetRawTextureData<byte>();
        try
        {
            unsafe
            {
                // Synchronously convert to the desired TextureFormat
                image.Convert(
                    conversionParams,
                    new IntPtr(NativeArrayUnsafeUtility.GetUnsafePtr(rawTextureData)),
                    rawTextureData.Length);

                SendImage(0, rawTextureData, rawTextureData.Length, image.width, image.height, currentResolution.width, currentResolution.height);
            }
        }
        finally
        {
            // Dispose the XRCpuImage after we're finished to prevent any memory leaks
            image.Dispose();
        }
    }
    


    /// <summary> 	
	/// Setup socket connection. 	
	/// </summary> 	
	private void ConnectToTcpServer()
    {
        try
        {
            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
        }
        catch (Exception e)
        {
            log.text += "On client connect exception " + e + "\n";
            Debug.Log("On client connect exception " + e);
        }
    }

    /// Runs in background clientReceiveThread; Listens for incomming data. 	
    /// </summary>     
    private void ListenForData()
    {
        try
        {
            socketConnection = new TcpClient(hostIP, hostPort);
            using (NetworkStream stream = socketConnection.GetStream())
            {
                Byte[] bytes = new Byte[1024];
                while (true)
                {
                    // Get a stream object for reading 				
                    byte[] datatype = new byte[4];
                    stream.Read(datatype, 0, 4);
                    int type = BitConverter.ToInt32(datatype, 0);
                    if (type == 1)
                    {
                        byte[] datalength = new byte[4];
                        stream.Read(datalength, 0, 4);
                        int length = BitConverter.ToInt32(datalength, 0);
                        Debug.Log("Length");
                        var incommingData = new byte[length];
                        stream.Read(incommingData, 0, length); // Read the actual message
                        string message = Encoding.UTF8.GetString(incommingData); // Convert bytes to string


                        //Vector2 touchPoint = StringToVector2(message);
                        //Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(touchPoint.x, touchPoint.y, Camera.main.nearClipPlane));
                        //log.text += "worldPoint: " + worldPoint + "\n";

                        //Debug.Log("worldPoint" +  worldPoint);
                        log.text += "Received Message: " + message + "\n";
                        Debug.Log("Received Message: " + message);
                        break; // Close socket after finish receiving message
                    }
                }
            }
        }
        catch (SocketException socketException)
        {
            log.text += "Socket exception: " + socketException + "\n";
            Debug.Log("Socket exception: " + socketException);
        }
    }

    /// <summary>
    ///  Very simple protocol:
    ///  4 bytes -> length
    /// </summary>
    /// <param name="rawImage"></param>
    /// <param name="length"></param>
    private void SendImage(int type, NativeArray<byte> rawImage, int length, int img_width, int img_height, int canvas_width, int canvas_height)
    {
        if (socketConnection == null)
        {
            return;
        }
        try
        {
            // Get a stream object for writing. 			
            NetworkStream stream = socketConnection.GetStream();
            if (stream.CanWrite) { 
            
                byte[] messageType = BitConverter.GetBytes(type);
                stream.Write(messageType, 0, messageType.Length);

                byte[] messageLength = BitConverter.GetBytes(length);
                stream.Write(messageLength, 0, messageLength.Length);

                byte[] imageWidth = BitConverter.GetBytes(img_width);
                stream.Write(imageWidth, 0, imageWidth.Length);

                byte[] imageHeight = BitConverter.GetBytes(img_height);
                stream.Write(imageHeight, 0, imageWidth.Length);

                byte[] canvasWidth = BitConverter.GetBytes(canvas_width);
                stream.Write(canvasWidth, 0, canvasWidth.Length);

                byte[] canvasHeight = BitConverter.GetBytes(canvas_height);
                stream.Write(canvasHeight, 0, canvasHeight.Length);

                byte[] imageBytes = rawImage.ToArray();
                stream.Write(imageBytes, 0, rawImage.Length);

                // Write byte array to socketConnection stream.                 
                log.text += "Client sent his message - should be received by server" + "\n";
                Debug.Log("Client sent his message - should be received by server");
            }
        }
        catch (SocketException socketException)
        {
            log.text += "Socket exception: " + socketException + "\n";
            Debug.Log("Socket exception: " + socketException);
        }
    }



    // Start is called before the first frame update
    void Start()
    {

        _camera = Camera.main;
        //ConnectToTcpServer();
        
    }

    void Update()
    {
        if(Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);

        if(touch.phase != TouchPhase.Began)
            return;

        Debug.Log(touch.position);
        log.text += touch.position + "\n";

        if (raycastManager.Raycast(touch.position, hits, TrackableType.FeaturePoint))
        {

            Pose hitPose = hits[0].pose;
            var anchor = anchorManager.AddAnchor(hitPose);
            

            if (anchor == null)
            {
                string errorEntry = "There was an error creating a reference point\n";
                Debug.Log(errorEntry);
            }
            else
            {
                Debug.Log("Added anchor");
                anchors.Add(anchor);
                Instantiate(cubePrefab, hitPose.position + new Vector3(0, 0.03f, 0), Quaternion.identity);
            }
        }
    }

}


