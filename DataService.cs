using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BlackBoxCmdb
{
    public class DataService
    {
        private readonly string _localPath = "data.json";
        private readonly string _bucketName;
        private readonly string _s3Key;
        private readonly IAmazonS3 _s3Client;

        public dynamic Data { get; private set; } // You can replace dynamic with a proper type

        public DataService(IAmazonS3 s3Client, string bucketName, string s3Key)
        {
            _s3Client = s3Client;
            _bucketName = bucketName;
            _s3Key = s3Key;
        }

        public async Task LoadAsync()
        {
            //if (File.Exists(_localPath))
            //{
            //    Console.WriteLine("Loading data from local file...");
            //    var json = await File.ReadAllTextAsync(_localPath);
            //    Data = JsonSerializer.Deserialize<dynamic>(json);
            //}
            //else
            //{
            //    Console.WriteLine("Local file not found. Loading from S3...");
            var stsClient = new AmazonSecurityTokenServiceClient(Amazon.RegionEndpoint.EUWest1);
            var newTempCreds = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
            {
                DurationSeconds = 3600,
                RoleArn = $"arn:aws:iam::649803627656:role/adam-subaccount-role",
                RoleSessionName = "Cmdb"
            });

            var client = new AmazonS3Client(newTempCreds.Credentials, Amazon.RegionEndpoint.EUWest1);

            var response = await client.GetObjectAsync(_bucketName, _s3Key);
            using var reader = new StreamReader(response.ResponseStream);
            var json = await reader.ReadToEndAsync();

            // Save locally for next time
            await File.WriteAllTextAsync(_localPath, json);

            Data = JsonSerializer.Deserialize<dynamic>(json);
        }
        //}
    }
}
