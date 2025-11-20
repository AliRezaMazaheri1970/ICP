using MediatR;
using Shared.Wrapper;
using System.IO; // اضافه شده برای Stream

namespace Application.Features.Samples.Commands.ImportSamples;

public record ImportSamplesCommand(Stream FileStream, string FileName) : IRequest<Result<int>>;