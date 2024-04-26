using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using static UnityEngine.XR.ARSubsystems.XRCpuImage;


public class HW3 : MonoBehaviour
{
    [SerializeField]
    String hostIP;
    [SerializeField] 
    int hostPort;

    [SerializeField]
    ARCameraManager cameraManager;

    [SerializeField]
    TMPro.TextMeshProUGUI log;

    private TcpClient socketConnection;
    private Thread clientReceiveThread;


    public void captureCameraImage() {


        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;


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

                SendImage(0,rawTextureData,rawTextureData.Length, image.width, image.height);
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
                    Debug.Log("Start Reading");
                    byte[] datatype = new byte[4];
                    stream.Read(datatype, 0, 4);
                    int type = BitConverter.ToInt32(datatype, 0);
                    Debug.Log("Datatype " + type.ToString());
                    if (type == 1)
                    {
                        byte[] datalength = new byte[4];
                        stream.Read(datalength, 0, 4);
                        int length = BitConverter.ToInt32(datalength, 0);
                        Debug.Log("Length");
                        var incommingData = new byte[length];
                        stream.Read(incommingData, 0, length); // Read the actual message
                        string message = Encoding.UTF8.GetString(incommingData); // Convert bytes to string
                        log.text += "Received Message: " + message + "\n";
                        Debug.Log("Received Message: " + message);
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
    private void SendImage(int type, NativeArray<byte> rawImage, int length, int width, int height)
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

                byte[] imageWidth = BitConverter.GetBytes(width);
                stream.Write(imageWidth, 0, imageWidth.Length);

                byte[] imageHeight = BitConverter.GetBytes(height);
                stream.Write(imageHeight, 0, imageWidth.Length);

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
        ConnectToTcpServer();
        
    }


}
