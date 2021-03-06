﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public interface IBaseCommandHandler
    {
        public abstract Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, object command);
    }

    public interface IBaseCommandHandler<T>
        where T : BaseCommand
    {
        public abstract Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, T command);
    }

    public abstract class BaseCommandHandler<T>: IBaseCommandHandler<T>, IBaseCommandHandler
        where T : BaseCommand
    {
        public abstract Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, T command);

        Task<BaseResponse> IBaseCommandHandler.Handle(InfoWebSocketConnection infoWebSocket, object command)
        {
            return Handle(infoWebSocket, (T)command);
        }
    }
}
