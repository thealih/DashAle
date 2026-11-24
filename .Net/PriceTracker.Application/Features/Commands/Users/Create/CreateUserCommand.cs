using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Application.Models.Shared;

namespace PriceTracker.Application.Features.Commands.Users.Create
{
    public class CreateUserCommand : IRequest<ResponseDTO>
    {
        public string UserName { get; set; }
    }

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, ResponseDTO>
    {
        public CreateUserCommandHandler()
        {

        }
        public Task<ResponseDTO> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
