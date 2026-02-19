using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Data.Sqlite;
using Mysqlx.Crud;
using MySqlX.XDevAPI;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text.Json;
using System.Xml.Linq;

namespace BlackBoxCmdb
{
    public class DataService
    {
        internal const string ConnectionString = "Data Source=Cmdb.db";
        //private const string ConnectionString = "Data Source=Cmdb;Mode=Memory;Cache=Shared";


        // This connection will stay open for the entire app lifetime
        internal readonly SqliteConnection PrimaryConnection = new SqliteConnection(ConnectionString);

        private string bucketName;

        public DataService(string bucketName)
        {
            this.bucketName = bucketName;
        }

        public dynamic Data { get; private set; }

        public async Task LoadAsync()
        {

            var stsClient = new AmazonSecurityTokenServiceClient(Amazon.RegionEndpoint.EUWest1);
            var newTempCreds = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
            {
                DurationSeconds = 3600,
                RoleArn = $"arn:aws:iam::649803627656:role/adam-subaccount-role",
                RoleSessionName = "Cmdb"
            });

            var client = new AmazonS3Client(newTempCreds.Credentials, Amazon.RegionEndpoint.EUWest1);

            var response = await client.GetObjectAsync(bucketName, "awesome.json");
            using var reader = new StreamReader(response.ResponseStream);
            var json = await reader.ReadToEndAsync();
            await PopulateTable("Accounts", true, "all-aws-accounts.json", client);
            await PopulateTable("Accounts", false, "all-aws-cn-accounts.json", client);
            await PopulateTable("Software", true, "cloud-ops-aws-ssm-software.json", client);
            await PopulateTable("Software", false, "cloud-ops-aws-cn-ssm-software.json", client);
            await PopulateTable("Ec2", true, "cloud-ops-aws-ec2-instances.json", client);
            await PopulateTable("Ec2", false, "cloud-ops-aws-cn-ec2-instances.json", client);

            Data = JsonSerializer.Deserialize<dynamic>(json);
        }

        private async Task PopulateTable(string tableName, bool dropTableFirst, string jsonFile, AmazonS3Client client)
        {
            PrimaryConnection.Open();


            var createCommand = PrimaryConnection.CreateCommand();

            if (dropTableFirst)
            {
                createCommand.CommandText = $"""DROP TABLE IF EXISTS [{tableName}];""";
                createCommand.ExecuteNonQuery();
                switch (tableName)
                {
                    case "Accounts":
                        createCommand.CommandText = $"""
CREATE TABLE [{tableName}] (
  [Id] bigint NOT NULL,
  [Name] text NULL,
  [Arn] text NULL,
  [AccountEmail] text NULL,
  [ContactEmail] text NULL,
  [JoinedTimestamp] text NULL,
  [SupportLevel] text NULL,
  [OwningTeam] text NULL,
  CONSTRAINT [sqlite_master_PK_{tableName}] PRIMARY KEY ([Id])
);
""";
                        break;
                    case "Software":
                        createCommand.CommandText = $"""
CREATE TABLE {tableName} (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Ec2InstanceId TEXT NOT NULL,
    Name TEXT NOT NULL,
    PackageId TEXT,
    Version TEXT,
    Architecture TEXT,
    Publisher TEXT,
    InstalledTime TEXT
);

""";
                        break;
                    case "Ec2":
                        createCommand.CommandText = $"""
CREATE TABLE [{tableName}] (
  [Id] text NOT NULL,
  [Name] text NULL,
  [PrivateIp] text NULL,
  [PlatformType] text NULL,
  [PlatformDetails] text NULL,
  [LaunchTime] text NULL,
  [AccountId] text NULL,
  [Region] text NULL,
  [OwningTeam] text NULL,
  [Product] text NULL,
  [Component] text NULL,
  [Environment] text NULL,
  CONSTRAINT [sqlite_master_PK_{tableName}] PRIMARY KEY ([Id])
);
""";
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown table name: {tableName}");
                }
                createCommand.ExecuteNonQuery();
            }


            createCommand.CommandText = """
DROP VIEW IF EXISTS SoftwareWithEc2;

CREATE VIEW SoftwareWithEc2 AS
SELECT
    e.AccountId,
    e.Region,
    s.Ec2InstanceId,
    s.Name,
    s.PackageId,
    s.Version,
    s.Architecture,
    s.Publisher,
    s.InstalledTime
FROM Software s
LEFT JOIN Ec2 e
    ON s.Ec2InstanceId = e.Id;
""";
            createCommand.ExecuteNonQuery();




            var accountsResponse = await client.GetObjectAsync(bucketName, jsonFile);
            using var stream = accountsResponse.ResponseStream; // directly from S3
            using var jsonDoc = await JsonDocument.ParseAsync(stream); // parses stream without reading all into a string

            using var transaction = PrimaryConnection.BeginTransaction();
            using var cmdInsert = PrimaryConnection.CreateCommand();

            switch (tableName)
            {
                case "Accounts":
                    cmdInsert.CommandText = $@"
INSERT INTO {tableName}
(Id, Name, Arn, AccountEmail, ContactEmail, JoinedTimestamp, SupportLevel, OwningTeam )
VALUES ($id, $name, $arn, $accountEmail, $contactEmail, $joinedTimestamp, $supportLevel, $owningTeam);";
                    cmdInsert.Parameters.Add("id", SqliteType.Text);
                    cmdInsert.Parameters.Add("$name", SqliteType.Text);
                    cmdInsert.Parameters.Add("$arn", SqliteType.Text);
                    cmdInsert.Parameters.Add("$accountEmail", SqliteType.Text);
                    cmdInsert.Parameters.Add("$contactEmail", SqliteType.Text);
                    cmdInsert.Parameters.Add("$joinedTimestamp", SqliteType.Text);
                    cmdInsert.Parameters.Add("$supportLevel", SqliteType.Text);
                    cmdInsert.Parameters.Add("$owningTeam", SqliteType.Text);
                    cmdInsert.Prepare();

                    // --- iterate instances ---
                    foreach (var item in jsonDoc.RootElement.EnumerateArray())
                    {

                        cmdInsert.Parameters["id"].Value = SafeGet(item, "AccountId");
                        cmdInsert.Parameters["$name"].Value = SafeGet(item, "Account.Name");
                        cmdInsert.Parameters["$arn"].Value = SafeGet(item, "Account.Arn");
                        cmdInsert.Parameters["$accountEmail"].Value = SafeGet(item, "Account.Email");
                        cmdInsert.Parameters["$contactEmail"].Value = SafeGet(item, "Tags.ContactEmail");
                        cmdInsert.Parameters["$joinedTimestamp"].Value = SafeGet(item, "Account.JoinedTimestamp");
                        cmdInsert.Parameters["$supportLevel"].Value = SafeGet(item, "SupportLevel");
                        cmdInsert.Parameters["$owningTeam"].Value = SafeGet(item, "Tags.OwningTeam");

                        cmdInsert.ExecuteNonQuery();
                    }

                    transaction.Commit();


                    break;
                case "Software":
                    cmdInsert.CommandText = $@"
INSERT INTO {tableName}
(Ec2InstanceId, Name, PackageId, Version, Architecture, Publisher, InstalledTime)
VALUES ($instanceId, $name, $packageId, $version, $arch, $publisher, $installed);";

                    cmdInsert.Parameters.Add("$instanceId", SqliteType.Text);
                    cmdInsert.Parameters.Add("$name", SqliteType.Text);
                    cmdInsert.Parameters.Add("$packageId", SqliteType.Text);
                    cmdInsert.Parameters.Add("$version", SqliteType.Text);
                    cmdInsert.Parameters.Add("$arch", SqliteType.Text);
                    cmdInsert.Parameters.Add("$publisher", SqliteType.Text);
                    cmdInsert.Parameters.Add("$installed", SqliteType.Text);
                    cmdInsert.Prepare();

                    // --- iterate instances ---
                    foreach (var item in jsonDoc.RootElement.EnumerateArray())
                    {
                        var instanceId = item.GetProperty("Ec2InstanceId").GetString() ?? "";

                        foreach (var software in item.GetProperty("SoftwareDetails").EnumerateArray())
                        {
                            cmdInsert.Parameters["$instanceId"].Value = instanceId;
                            cmdInsert.Parameters["$name"].Value = SafeGet(software, "Name");
                            cmdInsert.Parameters["$packageId"].Value = SafeGet(software, "PackageId");
                            cmdInsert.Parameters["$version"].Value = SafeGet(software, "Version");
                            cmdInsert.Parameters["$arch"].Value = SafeGet(software, "Architecture");
                            cmdInsert.Parameters["$publisher"].Value = SafeGet(software, "Publisher");
                            cmdInsert.Parameters["$installed"].Value = SafeGet(software, "InstalledTime");



                            cmdInsert.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                    break;
                case "Ec2":
                    cmdInsert.CommandText = $@"
INSERT INTO {tableName}
(Id,Name,PrivateIp,PlatformType,PlatformDetails,LaunchTime,AccountId,Region,OwningTeam,Product,Component,Environment)
VALUES ($id,$name,$privateIp,$platformType,$platformDetails,$launchTime,$accountId,$region,$owningTeam,$product,$component,$environment);";

                    cmdInsert.Parameters.Add("$id", SqliteType.Text);
                    cmdInsert.Parameters.Add("$name", SqliteType.Text);
                    cmdInsert.Parameters.Add("$privateIp", SqliteType.Text);
                    cmdInsert.Parameters.Add("$platformType", SqliteType.Text);
                    cmdInsert.Parameters.Add("$platformDetails", SqliteType.Text);
                    cmdInsert.Parameters.Add("$launchTime", SqliteType.Text);
                    cmdInsert.Parameters.Add("$accountId", SqliteType.Text);
                    cmdInsert.Parameters.Add("$region", SqliteType.Text);
                    cmdInsert.Parameters.Add("$owningTeam", SqliteType.Text);
                    cmdInsert.Parameters.Add("$product", SqliteType.Text);
                    cmdInsert.Parameters.Add("$component", SqliteType.Text);
                    cmdInsert.Parameters.Add("$environment", SqliteType.Text);
                    cmdInsert.Prepare();

                    // --- iterate instances ---
                    foreach (var item in jsonDoc.RootElement.EnumerateArray())
                    {
                        cmdInsert.Parameters["$id"].Value = SafeGet(item, "Ec2InstanceId");
                        cmdInsert.Parameters["$name"].Value = SafeGet(item, "Ec2InstanceName");
                        cmdInsert.Parameters["$privateIp"].Value = SafeGet(item, "PrivateIp");
                        cmdInsert.Parameters["$platformType"].Value = SafeGet(item, "PlatformType");
                        cmdInsert.Parameters["$platformDetails"].Value = SafeGet(item, "PlatformDetails");
                        cmdInsert.Parameters["$launchTime"].Value = SafeGet(item, "LaunchTime");
                        cmdInsert.Parameters["$accountId"].Value = SafeGet(item, "AccountId");
                        cmdInsert.Parameters["$region"].Value = SafeGet(item, "Region");
                        cmdInsert.Parameters["$owningTeam"].Value = SafeGet(item, "Tags.Team");
                        cmdInsert.Parameters["$product"].Value = SafeGet(item, "Tags.Product");
                        cmdInsert.Parameters["$component"].Value = SafeGet(item, "Tags.Component");
                        cmdInsert.Parameters["$environment"].Value = SafeGet(item, "Tags.Environment");
                        cmdInsert.ExecuteNonQuery();
                    }
                    transaction.Commit();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown table name: {tableName}");
            }




        }


        // Helper: safely get property or empty string
        static string SafeGet(JsonElement el, string path)
        {
            var parts = path.Split('.');
            JsonElement current = el;
            foreach (var p in parts)
            {
                if (!current.TryGetProperty(p, out var next))
                    return "";
                current = next;
            }
            return current.GetString() ?? "";
        }

    }
}
