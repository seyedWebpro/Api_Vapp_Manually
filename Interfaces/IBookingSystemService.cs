using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IBookingSystemService
    {
        Task<ApiResponse<BookingSystemListDto>> GetSystemsAsync(int userId, int pageNumber, int pageSize, bool? isActive);
        Task<ApiResponse<BookingSystemDto>> GetByIdAsync(int id, int userId);
        Task<ApiResponse<BookingSystemDto>> ToggleStatusAsync(int id, int userId);
        Task<ApiResponse<bool>> DeleteAsync(int id, int userId);
        Task<ApiResponse<BookingSystemDto>> UpdateAsync(int id, int userId, UpdateBookingSystemDto updateDto);

        Task<ApiResponse<List<BookingNotebookDto>>> GetNotebooksAsync(int userId);
        Task<ApiResponse<List<BookingActivityTypeDto>>> GetActivityTypesAsync();

        Task<ApiResponse<BookingStep1ValidationResponseDto>> ValidateStep1Async(int userId, BookingStep1Dto step1Dto);
        Task<ApiResponse<BookingStep2ValidationResponseDto>> ValidateStep2Async(int userId, BookingStep2Dto step2Dto);
        Task<ApiResponse<BookingStep3ValidationResponseDto>> ValidateStep3Async(int userId, BookingStep3Dto step3Dto);
        Task<ApiResponse<BookingStep4ValidationResponseDto>> ValidateStep4Async(int userId, BookingStep4Dto step4Dto);
        Task<ApiResponse<BookingSummaryDto>> GetSummaryAsync(int userId, string draftId);
        Task<ApiResponse<ConfirmBookingSystemResponseDto>> ConfirmAsync(int userId, ConfirmBookingSystemDto request);

        Task<ApiResponse<List<BookingServiceItemDto>>> GetServicesAsync(int systemId, int userId);
        Task<ApiResponse<BookingServiceItemDto>> AddServiceAsync(int systemId, int userId, AddBookingServiceDto dto);
        Task<ApiResponse<BookingServiceItemDto>> UpdateServiceAsync(int systemId, int serviceId, int userId, UpdateBookingServiceDto dto);
        Task<ApiResponse<bool>> DeleteServiceAsync(int systemId, int serviceId, int userId);
        Task<ApiResponse<BookingServiceItemDto>> GetServiceScheduleAsync(int systemId, int serviceId, int userId);
        Task<ApiResponse<BookingServiceItemDto>> SaveServiceScheduleAsync(int systemId, int serviceId, int userId, SaveBookingServiceScheduleDto dto);
        Task<ApiResponse<BookingScheduleExceptionDto>> AddScheduleExceptionAsync(int systemId, int serviceId, int userId, AddBookingScheduleExceptionDto dto);
        Task<ApiResponse<bool>> DeleteScheduleExceptionAsync(int systemId, int serviceId, int exceptionId, int userId);
    }
}
