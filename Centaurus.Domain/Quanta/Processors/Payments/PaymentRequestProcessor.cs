﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{
    public class PaymentRequestProcessor : QuantumRequestProcessor<PaymentProcessorContext>
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.PaymentRequest;

        public override PaymentProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new PaymentProcessorContext(container);
        }

        public override Task<ResultMessage> Process(PaymentProcessorContext context)
        {
            context.UpdateNonce();

            var payment = context.Payment;

            if (context.DestinationAccount == null)
            {
                var accId = Global.AccountStorage.GetNextAccountId();
                context.EffectProcessors.AddAccountCreate(Global.AccountStorage, accId, payment.Destination);
            }

            if (!context.DestinationAccount.Account.HasBalance(payment.Asset))
                context.EffectProcessors.AddBalanceCreate(context.DestinationAccount, payment.Asset);
            context.EffectProcessors.AddBalanceUpdate(context.DestinationAccount, payment.Asset, payment.Amount);

            context.EffectProcessors.AddBalanceUpdate(context.SourceAccount, payment.Asset, -payment.Amount);
            var effects = context.EffectProcessors.Effects;

            var accountEffects = effects.Where(e => e.AccountWrapper.Id == payment.Account).ToList();
            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        public override Task Validate(PaymentProcessorContext context)
        {
            context.ValidateNonce();

            var payment = context.Payment;

            if (payment.Destination == null || payment.Destination.IsZero())
                throw new BadRequestException("Destination should be valid public key");

            if (context.DestinationAccount == null)
            {
                if (payment.Asset != 0)
                    throw new BadRequestException("Account excepts only XLM asset.");
                if (payment.Amount < Global.Constellation.MinAccountBalance)
                throw new BadRequestException($"Min payment amount is {Amount.FromXdr(Global.Constellation.MinAccountBalance)} XLM for this account.");
            }

            if (payment.Destination.Equals(payment.AccountWrapper.Account.Pubkey))
                throw new BadRequestException("Source and destination must be different public keys");

            if (payment.Amount <= 0)
                throw new BadRequestException("Amount should be greater than 0");

            if (!Global.AssetIds.Contains(payment.Asset))
                throw new BadRequestException($"Asset {payment.Asset} is not supported");

            var balance = payment.AccountWrapper.Account.Balances.Find(b => b.Asset == payment.Asset);
            if (balance == null || !balance.HasSufficientBalance(payment.Amount))
                throw new BadRequestException("Insufficient funds");

            return Task.CompletedTask;
        }
    }
}
