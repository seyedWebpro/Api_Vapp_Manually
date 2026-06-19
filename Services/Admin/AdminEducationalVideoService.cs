using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminEducationalVideoService : IAdminEducationalVideoService
    {
        private readonly Api_Context _context;

        public AdminEducationalVideoService(Api_Context context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<EducationalVideoResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            var query = _context.EducationalVideos.AsNoTracking().Where(v => !v.IsDeleted);
            if (!includeInactive)
                query = query.Where(v => v.IsActive);

            var videos = await query.OrderBy(v => v.SortOrder).ThenByDescending(v => v.CreatedAt).ToListAsync();
            return ApiResponse<List<EducationalVideoResponseDto>>.CreateSuccess(videos.Select(Map).ToList());
        }

        public async Task<ApiResponse<EducationalVideoResponseDto>> GetByIdAsync(int id)
        {
            var video = await _context.EducationalVideos.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (video == null)
                return ApiResponse<EducationalVideoResponseDto>.NotFound("ویدیو یافت نشد");

            return ApiResponse<EducationalVideoResponseDto>.CreateSuccess(Map(video));
        }

        public async Task<ApiResponse<EducationalVideoResponseDto>> CreateAsync(CreateEducationalVideoDto dto)
        {
            var video = new EducationalVideo
            {
                Title = dto.Title.Trim(),
                Description = dto.Description?.Trim(),
                VideoUrl = dto.VideoUrl.Trim(),
                ThumbnailUrl = dto.ThumbnailUrl?.Trim(),
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.EducationalVideos.Add(video);
            await _context.SaveChangesAsync();
            return ApiResponse<EducationalVideoResponseDto>.CreateSuccess(Map(video), "ویدیو ایجاد شد", 201);
        }

        public async Task<ApiResponse<EducationalVideoResponseDto>> UpdateAsync(int id, UpdateEducationalVideoDto dto)
        {
            var video = await _context.EducationalVideos.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (video == null)
                return ApiResponse<EducationalVideoResponseDto>.NotFound("ویدیو یافت نشد");

            video.Title = dto.Title.Trim();
            video.Description = dto.Description?.Trim();
            video.VideoUrl = dto.VideoUrl.Trim();
            video.ThumbnailUrl = dto.ThumbnailUrl?.Trim();
            video.SortOrder = dto.SortOrder;
            video.IsActive = dto.IsActive;
            video.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ApiResponse<EducationalVideoResponseDto>.CreateSuccess(Map(video), "ویدیو به‌روزرسانی شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            var video = await _context.EducationalVideos.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (video == null)
                return ApiResponse<bool>.NotFound("ویدیو یافت نشد");

            video.IsDeleted = true;
            video.IsActive = false;
            video.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.CreateSuccess(true, "ویدیو حذف شد");
        }

        public async Task<ApiResponse<List<EducationalVideoResponseDto>>> GetActiveVideosAsync()
        {
            var videos = await _context.EducationalVideos.AsNoTracking()
                .Where(v => v.IsActive && !v.IsDeleted)
                .OrderBy(v => v.SortOrder)
                .ToListAsync();
            return ApiResponse<List<EducationalVideoResponseDto>>.CreateSuccess(videos.Select(Map).ToList());
        }

        private static EducationalVideoResponseDto Map(EducationalVideo video) => new()
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            VideoUrl = video.VideoUrl,
            ThumbnailUrl = video.ThumbnailUrl,
            SortOrder = video.SortOrder,
            IsActive = video.IsActive,
            CreatedAt = video.CreatedAt,
            UpdatedAt = video.UpdatedAt
        };
    }
}
