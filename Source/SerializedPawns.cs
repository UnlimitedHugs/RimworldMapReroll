using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using RimWorld.Planet;
using Verse;

namespace MapReroll {
	/**
	 * Converts a list of pawns into their XML representation and back.
	 * This allows us to always have the same pristine starting pawns after a reroll, regardless of what happened to them on the previous map.
	 */
	public class SerializedPawns {
		private readonly FieldInfo scribePrivateWriter = typeof(Scribe).GetField("writer", BindingFlags.Static | BindingFlags.NonPublic);
		private readonly string xmlData;
		private List<Pawn> offMapRelatives; 

		public SerializedPawns(List<Pawn> pawns) {
			if (scribePrivateWriter == null) throw new Exception("Failed to reflect Scribe.writer");
			xmlData = Serialize(pawns);
		}

		public List<Pawn> ToList() {
			return Unserialize(xmlData);
		}

		public List<Pawn> GetOffMapRelatives() {
			return offMapRelatives;
		} 

		private string Serialize(List<Pawn> pawns) {
			offMapRelatives = new List<Pawn>();
			// unserialization requires references to off-map pawns. While loading a normal save those pawns are unserialized together,
			// here we have to add them manually.
			foreach (var pawn in pawns) {
				foreach (var relation in pawn.relations.DirectRelations) {
					if (!relation.otherPawn.IsWorldPawn()) continue;
					if(!offMapRelatives.Contains(relation.otherPawn)) offMapRelatives.Add(relation.otherPawn);
				}
			}
			using (var stringWriter = new StringWriter()) {
				XmlWriter writer;
				using (writer = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = false, IndentChars = "", OmitXmlDeclaration = true })) {
					DebugLoadIDsSavingErrorsChecker.Clear();
					var prevWriter = scribePrivateWriter.GetValue(null);
					var prevMode = Scribe.mode;
					try {
						scribePrivateWriter.SetValue(null, writer);
						Scribe.mode = LoadSaveMode.Saving;
						Scribe_Collections.LookList(ref pawns, "list", LookMode.Deep);
						return stringWriter.ToString();
					} catch (Exception e) {
						throw new Exception("Failed to serialize starting pawns. Exception was: " + e);
					} finally {
						Scribe.mode = prevMode;
						scribePrivateWriter.SetValue(null, prevWriter);
					}
				}
			}
		}

		private List<Pawn> Unserialize(string xml) {
			// necessary for proper cross-referencing after restoring pawns
			foreach (var pawn in offMapRelatives) {
				try {
					LoadedObjectDirectory.RegisterLoaded(pawn);
				} catch (Exception) {
					// a precaution against already registered pawns, just in case.
				}
			}
			var xmlDocument = new XmlDocument();
			xmlDocument.LoadXml(xml);
			Scribe.curParent = xmlDocument;
			var prevMode = Scribe.mode;
			Scribe.mode = LoadSaveMode.LoadingVars;
			try {
				List<Pawn> list = null;
				Scribe_Collections.LookList(ref list, "list", LookMode.Deep);
				CrossRefResolver.ResolveAllCrossReferences();
				PostLoadInitter.DoAllPostLoadInits();
				DebugLoadIDsSavingErrorsChecker.Clear();
				return list;
			} catch (Exception e) {
				throw new Exception("Failed to unserialize starting pawns from XML. Exception was: " + e + "\n XML was: " + xml);
			} finally {
				Scribe.mode = prevMode;
			}
		}
	}
}