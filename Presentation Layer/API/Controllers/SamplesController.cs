using Core.Icp.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Presentation.Icp.API.Models;
using Shared.Icp.DTOs.Samples;
using Shared.Icp.Helpers.Mappers;

namespace Presentation.Icp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SamplesController : ControllerBase
    {
        private readonly ISampleService _sampleService;
        private readonly ILogger<SamplesController> _logger;

        public SamplesController(
            ISampleService sampleService,
            ILogger<SamplesController> logger)
        {
            _sampleService = sampleService;
            _logger = logger;
        }

        /// <summary>
        /// همه نمونه‌ها.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<SampleDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<SampleDto>>>> GetAll()
        {
            _logger.LogInformation("Getting all samples");

            var samples = await _sampleService.GetAllSamplesAsync();
            var dtos = samples.ToDtoList();

            return Ok(ApiResponse<List<SampleDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// نمونه‌های یک پروژه.
        /// </summary>
        [HttpGet("by-project/{projectId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<List<SampleDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<SampleDto>>>> GetByProject(Guid projectId)
        {
            _logger.LogInformation("Getting samples for project {ProjectId}", projectId);

            var samples = await _sampleService.GetSamplesByProjectIdAsync(projectId);
            var dtos = samples.ToDtoList();

            return Ok(ApiResponse<List<SampleDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// یک نمونه بر اساس Id.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<SampleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SampleDto>>> GetById(Guid id)
        {
            _logger.LogInformation("Getting sample {SampleId}", id);

            var sample = await _sampleService.GetSampleByIdAsync(id);
            if (sample is null)
                return NotFound(ApiResponse<SampleDto>.FailureResponse("نمونه یافت نشد"));

            return Ok(ApiResponse<SampleDto>.SuccessResponse(sample.ToDto()));
        }

        /// <summary>
        /// ایجاد نمونه جدید.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<SampleDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<SampleDto>>> Create([FromBody] CreateSampleDto dto)
        {
            _logger.LogInformation("Creating new sample: {SampleName}", dto.SampleName);

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<SampleDto>.FailureResponse("داده‌های ورودی نامعتبر است"));

            var sample = dto.ToEntity();
            sample = await _sampleService.CreateSampleAsync(sample);

            var resultDto = sample.ToDto();

            return CreatedAtAction(
                nameof(GetById),
                new { id = sample.Id },
                ApiResponse<SampleDto>.SuccessResponse(resultDto, "نمونه با موفقیت ایجاد شد"));
        }

        /// <summary>
        /// ویرایش نمونه.
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<SampleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SampleDto>>> Update(
            Guid id,
            [FromBody] UpdateSampleDto dto)
        {
            _logger.LogInformation("Updating sample {SampleId}", id);

            var sample = await _sampleService.GetSampleByIdAsync(id);
            if (sample is null)
                return NotFound(ApiResponse<SampleDto>.FailureResponse("نمونه یافت نشد"));

            sample.UpdateFromDto(dto);

            sample = await _sampleService.UpdateSampleAsync(sample);

            return Ok(ApiResponse<SampleDto>.SuccessResponse(sample.ToDto(), "نمونه با موفقیت به‌روزرسانی شد"));
        }

        /// <summary>
        /// حذف نرم نمونه.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            _logger.LogInformation("Deleting sample {SampleId}", id);

            var deleted = await _sampleService.DeleteSampleAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<object>.FailureResponse("نمونه یافت نشد"));

            return NoContent();
        }
    }
}
