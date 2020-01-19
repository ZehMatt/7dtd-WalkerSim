using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace WalkerSim.Viewer
{
	enum DataType
	{
		MapData,
	}

	interface IWalkerSimMessage
	{
		void Serialize(Stream stream);
		void Deserialize(Stream stream);
	}
}