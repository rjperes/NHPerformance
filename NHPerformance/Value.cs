using System;

namespace NHPerformance
{
	public class Value
	{
		public virtual Int32 ValueId { get; set; }
		public virtual DateTime Timestamp { get; set; }
		public virtual Device Device { get; set; }
		public virtual Measure Measure { get; set; }
		public virtual Double Val { get; set; }
	}
}
