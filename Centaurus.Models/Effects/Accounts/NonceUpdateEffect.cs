﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class NonceUpdateEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.NonceUpdate;
        
        [XdrField(0)]
        public ulong Nonce { get; set; }

        [XdrField(1)]
        public ulong PrevNonce { get; set; }
    }
}
