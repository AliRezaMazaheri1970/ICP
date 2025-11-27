using MediatR;
using Shared.Wrapper;

namespace Application.Features.Samples.Commands.ImportSamples;

public class ImportSamplesCommand : IRequest<Result<int>>
{
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public Stream FileStream { get; set; } = default!;
}