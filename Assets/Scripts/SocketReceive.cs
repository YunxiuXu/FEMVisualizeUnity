using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Collections.Generic;


public class SocketReceive : MonoBehaviour
{
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    public GameObject sphere;
    public GameObject cylinderPrefab;
    private GameObject[] spheres;  // 用于保存所有球体实例的数组
    private List<GameObject> cylinders = new List<GameObject>();
    //声明一个Vector3数组
    private Vector3[] resultVectors, resultLineVectors;
    private LineRenderer lineRenderer;  // 线渲染器
    public Material lineMaterial;  // 线材质
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    public int TetraSetsize = 432;//Tetra Set size : 
    void Start()
    {
        int tetrahedronCount = TetraSetsize * 4 / 4; //去C++事前计算开头看

        for (int i = 0; i < tetrahedronCount; i++)
        {
            GameObject tetrahedron = new GameObject("Tetrahedron" + i);
            tetrahedron.transform.SetParent(transform);
            LineRenderer lineRenderer = tetrahedron.AddComponent<LineRenderer>();
            lineRenderer.material = lineMaterial;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 12;
            lineRenderers.Add(lineRenderer);
        }

        spheres = new GameObject[TetraSetsize];
        for (int i = 0; i < TetraSetsize; i++)
        {
            spheres[i] = Instantiate(sphere);
        }

        for (int i = 0; i < tetrahedronCount * 6; i++)
        {
            GameObject cylinder = Instantiate(cylinderPrefab);
            cylinders.Add(cylinder);
        }

        udpClient = new UdpClient(9003);  // 监听端口9003
        remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        
        BeginReceive();

        
    }
    void Update(){
        if(resultVectors != null){
            int cylinderIndex = 0;
            for (int i = 0; i < resultLineVectors.Length; i += 4)
            {
                UpdateEdges(resultLineVectors[i], resultLineVectors[i + 1], resultLineVectors[i + 2], resultLineVectors[i + 3], ref cylinderIndex);
            }
            for (int j = 0; j < resultVectors.Length; j++)
            {
                // 在这里处理每个Vector3对象
                //print(resultVectors[i]);
                if(j < TetraSetsize)
                    spheres[j].transform.position = resultVectors[j];
            }

            // // 更新LineRenderer的所有点的位置以匹配球体的位置
            // if (resultLineVectors.Length % 4 != 0)
            // {
            //     Debug.LogError("resultLineVectors count is not a multiple of 4!");
            //     return;
            // }

            // DrawAllTetrahedrons();
        }
        
    }

    void UpdateEdges(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, ref int cylinderIndex)
    {
        UpdateCylinder(cylinders[cylinderIndex++], v1, v2);
        UpdateCylinder(cylinders[cylinderIndex++], v1, v3);
        UpdateCylinder(cylinders[cylinderIndex++], v1, v4);
        UpdateCylinder(cylinders[cylinderIndex++], v2, v3);
        UpdateCylinder(cylinders[cylinderIndex++], v2, v4);
        UpdateCylinder(cylinders[cylinderIndex++], v3, v4);
    }

    void UpdateCylinder(GameObject cylinder, Vector3 start, Vector3 end)
    {
        Vector3 offset = end - start;
        Vector3 position = start + offset / 2.0f;
        float length = offset.magnitude;

        cylinder.transform.position = position;
        cylinder.transform.up = offset;
        cylinder.transform.localScale = new Vector3(cylinder.transform.localScale.x, length / 2.0f, cylinder.transform.localScale.z);
    }
    void DrawAllTetrahedrons()
    {
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            Vector3 v0 = resultLineVectors[i * 4];
            Vector3 v1 = resultLineVectors[i * 4 + 1];
            Vector3 v2 = resultLineVectors[i * 4 + 2];
            Vector3 v3 = resultLineVectors[i * 4 + 3];

            lineRenderers[i].SetPositions(new Vector3[] {
                v0, v1, v1, v2, v2, v0,
                v0, v3, v1, v3, v2, v3
            });
        }
    }

    

    private void BeginReceive()
    {
        udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        byte[] receivedBytes = udpClient.EndReceive(ar, ref remoteEndPoint);
        string receivedString = Encoding.UTF8.GetString(receivedBytes);
        
        // 处理接收到的数据
        ProcessReceivedData(receivedString);

        // 继续监听下一份数据
        BeginReceive();
    }

    private void ProcessReceivedData(string data)
    {
        // 在这里处理接收到的数据，例如解析3D图形数据并更新Mesh对象
        resultLineVectors = DeserializeVectors(data);
        data = RemoveDuplicateCoordinates(data);
        resultVectors = DeserializeVectors(data);
    }

    void OnDestroy()
    {
        udpClient.Close();
    }

    public Vector3[] DeserializeVectors(string serializedData)
    {
        string[] vectorStrings = serializedData.TrimEnd(';').Split(';');  // 删除最后一个分号，然后根据分号分割字符串
        List<Vector3> vectors = new List<Vector3>();

        foreach (string vectorString in vectorStrings)
        {
            string[] coordinates = vectorString.Split(',');  // 根据逗号分割每个向量字符串
            if (coordinates.Length == 3)
            {
                float x, y, z;
                if (float.TryParse(coordinates[0], out x) &&
                    float.TryParse(coordinates[1], out y) &&
                    float.TryParse(coordinates[2], out z))
                {
                    vectors.Add(new Vector3(x, -y, z)); //因为C++那边y是向下的
                }
                else
                {
                    Debug.LogError("Failed to parse vector: " + vectorString);
                }
            }
            else
            {
                Debug.LogError("Invalid vector format: " + vectorString);
            }
        }

        return vectors.ToArray();
    }

    public string RemoveDuplicateCoordinates(string serializedData)
    {
        Dictionary<string, bool> uniqueCoordinates = new Dictionary<string, bool>();
        string[] vectorStrings = serializedData.TrimEnd(';').Split(';');
        string newSerializedData = "";

        foreach (string vectorString in vectorStrings)
        {
            if (!uniqueCoordinates.ContainsKey(vectorString))
            {
                uniqueCoordinates.Add(vectorString, true);
                newSerializedData += vectorString + ";";
            }
        }

        return newSerializedData;
    }
}
