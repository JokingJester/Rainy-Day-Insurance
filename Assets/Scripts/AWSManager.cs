using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.IO;
using System;
using Amazon.S3.Util;
using System.Collections.Generic;
using Amazon.CognitoIdentity;
using Amazon;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

public class AWSManager : MonoBehaviour
{
    private static AWSManager _instance;
    public static AWSManager Instance
    {
        get
        {
            if(_instance == null)
            {
                Debug.LogError("AWS Instance is null");
            }
            return _instance;
        }
    }
    public string S3Region = RegionEndpoint.USEast2.SystemName;
    private RegionEndpoint _S3Region
    {
        get { return RegionEndpoint.GetBySystemName(S3Region); }
    }

    private AmazonS3Client _s3Client;
    public AmazonS3Client S3Client
    {
        get
        {
            if(_s3Client == null)
            {
                _s3Client = new AmazonS3Client(new CognitoAWSCredentials(
                "us-east-2:9a543040-f461-4401-8dce-82df68c2230f", // Identity Pool ID
                RegionEndpoint.USEast2),_S3Region);
            }
            return _s3Client;
        }
    }

    private void Awake()
    {
        _instance = this;

        UnityInitializer.AttachToGameObject(this.gameObject);
        AWSConfigs.HttpClient = AWSConfigs.HttpClientOption.UnityWebRequest;

        /* ResultText is a label used for displaying status information
        S3Client.ListBucketsAsync(new ListBucketsRequest(), (responseObject) =>
        {
            if (responseObject.Exception == null)
            {
                responseObject.Response.Buckets.ForEach((s3b) =>
                {
                    Debug.Log("Bucket Name: " + s3b.BucketName);
                });
            }
            else
            {
                Debug.Log("AWS Error " + responseObject.Exception);
            }
        });
        */
    }

    public void UploadToAWS(string filePath, string caseID)
    {
        FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        PostObjectRequest request = new PostObjectRequest()
        {
            Bucket = "casefilesinsuranceapp",
            Key = "case#" + caseID,
            InputStream = stream,
            CannedACL = S3CannedACL.Private,
            Region = _S3Region
        };

        S3Client.PostObjectAsync(request, (responseObj) => 
        {
            if(responseObj.Exception == null)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                Debug.Log("Exception occured during upload " + responseObj.Exception);
            }
        });
    }

    public void GetList(string caseNumber, Action onComplete = null)
    {
        Debug.Log("Getting List");
        string target = "case#" + caseNumber;
        var request = new ListObjectsRequest()
        {
            BucketName = "casefilesinsuranceapp"
        };

        S3Client.ListObjectsAsync(request, (responseObj) =>
        {
            if (responseObj.Exception == null)
            {
                bool caseFound = responseObj.Response.S3Objects.Any(obj => obj.Key == target);
                if(caseFound == true)
                {
                    Debug.Log("Case Found");
                    S3Client.GetObjectAsync("casefilesinsuranceapp", target, (responseObj) =>
                    {
                        //read the data and apply case object to be used

                        //check if response stream is null
                        if(responseObj.Response != null)
                        {
                            //byte array to store data from file
                            byte[] data = null;

                            //use stream reader to read response data
                            using (StreamReader reader = new StreamReader(responseObj.Response.ResponseStream))
                            {
                                //access a memory stream
                                using(MemoryStream memory = new MemoryStream())
                                {
                                    //populate data byte array with the memstream stream data
                                    var buffer = new byte[512];
                                    var bytesRead = default(int);

                                    while((bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        memory.Write(buffer, 0, bytesRead);
                                    }
                                    data = memory.ToArray();
                                }
                            }

                            //convert those bytes to a case
                            using (MemoryStream memory = new MemoryStream(data))
                            {
                                BinaryFormatter bf = new BinaryFormatter();
                                Case downloadedCase = bf.Deserialize(memory) as Case;
                                UIManager.Instance.activeCase = downloadedCase;

                                if (onComplete != null)
                                    onComplete();
                            }

                        }
                    });
                }
                else
                {
                    Debug.Log("Case Not Found");
                }
            }
            else
            {
                Debug.Log("Error getting list of items " + responseObj.Exception);
            }
        });
    }
}
