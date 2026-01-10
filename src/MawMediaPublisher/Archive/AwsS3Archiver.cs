using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using CliWrap;
using MawMediaPublisher.Models;
using MawMediaPublisher.Scale;
using Spectre.Console;

namespace MawMediaPublisher.Archive;

// https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/sso-tutorial-app-only.html
class AwsS3Archiver
{
    // creds stored in ~/.aws/config
    // note that the first "default" section seems pretty important, so do not remove that
    const string SSO_CREDS = "mawpower";
    const string S3_BUCKET = "mikeandwan-us-assets";

    AmazonS3Client? _client;

    public void Authenticate()
    {
        var ssoCreds = LoadSsoCredentials(SSO_CREDS);

        _client = new AmazonS3Client(ssoCreds);
    }

    public async Task ArchiveMedia(Category category, MediaFile media)
    {
        var localPath = Path.Combine(category.LocalMediaPath, ScaleSpec.Src.Code, Path.GetFileName(media.OriginalFilepath));

        await ArchiveFile(category, localPath);
    }

    public async Task ArchivePp3(Category category, FileInfo pp3File)
    {
        await ArchiveFile(category, pp3File.FullName);
    }

    async Task ArchiveFile(Category category, string localFilePath)
    {
        if (_client == null)
        {
            throw new ApplicationException("You must first authenticate with AWS before trying to archive media!");
        }

        var key = $"{category.EffectiveDate.Year}/{category.BaseDirectoryName}/{Path.GetFileName(localFilePath)}";

        var putRequest = new PutObjectRequest
        {
            BucketName = S3_BUCKET,
            Key = key,
            FilePath = localFilePath,
            StorageClass = S3StorageClass.DeepArchive
        };

        await _client.PutObjectAsync(putRequest);
    }

    static AWSCredentials LoadSsoCredentials(string profile)
    {
        var chain = new CredentialProfileStoreChain();

        if (!chain.TryGetAWSCredentials(profile, out var credentials))
        {
            throw new Exception($"Failed to find the {profile} profile in the AWS configuration");
        }

        var ssoCredentials = credentials as SSOAWSCredentials;

        if (ssoCredentials == null)
        {
            throw new Exception("Did not obtain valid SSO credentials for AWS");
        }

        ssoCredentials.Options.ClientName = "MawMediaPublisher";
        ssoCredentials.Options.SsoVerificationCallback = async args =>
        {
            AnsiConsole.MarkupLineInterpolated($"[bold green]Please verify the following code matches what is in the browser before logging in:[/] [bold yellow]{args.UserCode}[/]");

            await Cli
                .Wrap("xdg-open")
                .WithArguments(args.VerificationUriComplete)
                .ExecuteAsync();
        };

        ssoCredentials.Options.SupportsGettingNewToken = true;

        return ssoCredentials;
    }
}
