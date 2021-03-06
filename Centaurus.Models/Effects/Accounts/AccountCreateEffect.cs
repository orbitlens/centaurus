﻿using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class AccountCreateEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.AccountCreate;

        public RawPubKey Pubkey { get; set; }

        public int AccountId { get; set; }
    }
}
