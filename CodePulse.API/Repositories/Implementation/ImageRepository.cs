using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CodePulse.API.Data;
using CodePulse.API.Models.Domain;
using CodePulse.API.Models.DTO;
using CodePulse.API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CodePulse.API.Repositories.Implementation
{
    public class ImageRepository : IImageRepository
    {
        private readonly Cloudinary cloudinary;
        private readonly ApplicationDbContext dbContext;

        public ImageRepository(IOptions<CloudinarySettings> cloudinaryOptions, ApplicationDbContext dbContext)
        {
            var settings = cloudinaryOptions.Value;
            this.cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
            this.dbContext = dbContext;
        }

        public async Task<IEnumerable<BlogImage>> GetAll()
        {
            return await dbContext.BlogImages.ToListAsync();
        }

        public async Task<BlogImage> Upload(IFormFile file, BlogImage blogImage)
        {
            await using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription($"{blogImage.FileName}{blogImage.FileExtension}", stream),
                PublicId = $"{blogImage.FileName}-{Guid.NewGuid()}",
                Overwrite = false
            };

            var uploadResult = await cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            blogImage.Url = uploadResult.SecureUrl.AbsoluteUri;

            await dbContext.BlogImages.AddAsync(blogImage);
            await dbContext.SaveChangesAsync();

            return blogImage;
        }
    }
}
