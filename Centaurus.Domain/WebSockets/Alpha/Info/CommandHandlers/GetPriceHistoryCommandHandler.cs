﻿using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class GetPriceHistoryCommandHandler : BaseCommandHandler<GetPriceHistoryCommand>
    {
        public override async Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, GetPriceHistoryCommand command)
        {
            var asset = Global.Constellation.Assets.FirstOrDefault(a => a.Id == command.Market);
            if (asset == null && asset.IsXlm)
                throw new BadRequestException("Invalid market.");

            var res = await Global.AnalyticsManager.PriceHistoryManager.GetPriceHistory(command.Cursor, command.Market, command.Period);
            return new PriceHistoryResponse  { 
                RequestId = command.RequestId,
                PriceHistory  = res.frames,
                NextCursor = res.nextCursor
            };
        }
    }
}
