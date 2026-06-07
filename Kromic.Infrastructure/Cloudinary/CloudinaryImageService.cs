using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Cloudinary;

public sealed class CloudinaryImageService : ICloudinaryImageService
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;

    public CloudinaryImageService(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
        _cloudinary = new CloudinaryDotNet.Cloudinary(new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret));
    }

    public async Task<Kromic.Application.Interfaces.ImageUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Image file is empty.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = _options.Folder,
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        }, cancellationToken);

        if (result.Error is not null)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        return new Kromic.Application.Interfaces.ImageUploadResult(result.PublicId, result.SecureUrl?.ToString() ?? result.Url.ToString());
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return;
        }

        await _cloudinary.DestroyAsync(new DeletionParams(publicId));
    }
}
