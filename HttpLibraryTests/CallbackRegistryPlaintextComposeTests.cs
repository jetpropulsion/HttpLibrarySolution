using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class CallbackRegistryPlaintextComposeTests
	{
		[TestMethod]
		public void PlaintextStreamFilters_AreComposed_InRegistrationOrder()
		{
			CallbackRegistry.Clear();
			string clientName = "compose-client";

			// Register first filter and capture its delegate instance
			SocketCallbackHandlers temp1 = new SocketCallbackHandlers();
			temp1.PlaintextStreamFilter = (context, ct) =>
			{
				return new ValueTask<Stream>(context.PlaintextStream);
			};
			CallbackRegistry.RegisterHandlers(clientName, temp1);
			Delegate? firstDelegate = temp1.PlaintextStreamFilter as Delegate;
			Assert.IsNotNull(firstDelegate);

			// Register second filter using a temporary handlers object so we can capture its delegate instance
			SocketCallbackHandlers temp2 = new SocketCallbackHandlers();
			temp2.PlaintextStreamFilter = (context, ct) =>
			{
				return new ValueTask<Stream>(context.PlaintextStream);
			};
			CallbackRegistry.RegisterHandlers(clientName, temp2);
			Delegate? secondDelegate = temp2.PlaintextStreamFilter as Delegate;
			Assert.IsNotNull(secondDelegate);

			SocketCallbackHandlers? merged = CallbackRegistry.GetHandlers(clientName);
			Assert.IsNotNull(merged);
			Assert.IsNotNull(merged!.PlaintextStreamFilter);

			Assert.IsTrue(merged.PlaintextFilterIsComposed, "Plaintext filter should be marked as composed");

			Delegate composed = merged.PlaintextStreamFilter as Delegate;
			Assert.IsNotNull(composed);

			object? target = composed.Target;
			Assert.IsNotNull(target, "Composed delegate should have a target closure");

			FieldInfo[] fields = target!.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			bool foundFirst = false;
			bool foundSecond = false;
			foreach(FieldInfo f in fields)
			{
				object? v = f.GetValue(target);
				if(object.ReferenceEquals(v, firstDelegate))
					foundFirst = true;
				if(object.ReferenceEquals(v, secondDelegate))
					foundSecond = true;
			}

			Assert.IsTrue(foundFirst, "Composed closure should contain reference to first filter delegate");
			Assert.IsTrue(foundSecond, "Composed closure should contain reference to second filter delegate");
		}
	}
}