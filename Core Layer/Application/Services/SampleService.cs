using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Interfaces.Repositories;
using Core.Icp.Domain.Interfaces.Services;

namespace Core.Icp.Application.Services.Samples
{
    /// <summary>
    /// پیاده‌سازی سرویس Sample ها با استفاده از UnitOfWork و Repository.
    /// </summary>
    public class SampleService : ISampleService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SampleService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<Sample>> GetAllSamplesAsync()
        {
            return await _unitOfWork.Samples.GetAllAsync();
        }

        public async Task<IEnumerable<Sample>> GetSamplesByProjectIdAsync(Guid projectId)
        {
            return await _unitOfWork.Samples.GetByProjectIdAsync(projectId);
        }

        public async Task<Sample?> GetSampleByIdAsync(Guid id)
        {
            return await _unitOfWork.Samples.GetByIdAsync(id);
        }

        public async Task<Sample> CreateSampleAsync(Sample sample)
        {
            await _unitOfWork.Samples.AddAsync(sample);
            await _unitOfWork.SaveChangesAsync();
            return sample;
        }

        public async Task<Sample> UpdateSampleAsync(Sample sample)
        {
            await _unitOfWork.Samples.UpdateAsync(sample);
            await _unitOfWork.SaveChangesAsync();
            return sample;
        }

        public async Task<bool> DeleteSampleAsync(Guid id)
        {
            var entity = await _unitOfWork.Samples.GetByIdAsync(id);
            if (entity is null)
                return false;

            await _unitOfWork.Samples.DeleteAsync(entity);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
