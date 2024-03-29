﻿/*
 * 2012 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Glenn Desmadryl
 */
using System;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// Stores a parameter for a client, for example IP or username.
    /// </summary>
    [Serializable]
    public class Parameter {
        private object _value;
        /// <summary>
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// </summary>
        public string Description { get; set; }
        ///// <summary>
        ///// For auto-generated gui.
        ///// </summary>
        //public Type Type { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool Optional { get; set; }
        /// <summary>
        /// Gets the default value if value is null.
        /// </summary>
        public object Value {
            get { return _value ?? DefaultValue; }
            set {
                if (value.GetType() != DefaultValue.GetType())
                    throw new Exception("Value must be of the same type as DefaultValue.");
                _value = value;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public object DefaultValue { get; set; }
        /// <summary>
        /// For passwords.
        /// </summary>
        public bool Encrypted { get; set; }
    }
}
