using Microsoft.AspNetCore.Http;

namespace Kromic.Application.Interfaces;

public sealed record ImageUploadResult(string PublicId, string Url);

public interface ICloudinaryImageService
{
    Task<ImageUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}
