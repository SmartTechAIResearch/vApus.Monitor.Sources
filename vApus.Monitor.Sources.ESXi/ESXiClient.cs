/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.ESXi
{
    public class ESXiClient : BasePollingClient
    {
        protected override Entities PollCounters() {
            throw new NotImplementedException();
        }

        public override bool IsConnected {
            get { throw new NotImplementedException(); }
        }

        public override int RefreshCountersInterval {
            get { throw new NotImplementedException(); }
        }

        public override string DecimalSeparator {
            get { throw new NotImplementedException(); }
        }

        public override Entities WDYH {
            get { throw new NotImplementedException(); }
        }

        public override bool Connect() {
            throw new NotImplementedException();
        }

        public override bool Disconnect() {
            throw new NotImplementedException();
        }
    }
}
