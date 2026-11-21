#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace lilToon
{
    // Library storage keeps diagnostic backups out of the project's asset database.
    [FilePath("Library/lilToon/PresetDebugSession.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class lilPresetDebugSession : ScriptableSingleton<lilPresetDebugSession>
    {
        [Serializable]
        private sealed class ObjectReference
        {
            public string propertyPath;
            public Object value;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public Material material;
            public string json;
            public List<ObjectReference> references = new List<ObjectReference>();
        }

        [SerializeField] private List<Snapshot> snapshots = new List<Snapshot>();

        static lilPresetDebugSession()
        {
            Undo.undoRedoPerformed += SaveAfterUndo;
        }

        public static bool Contains(Material material)
        {
            return material != null && instance.snapshots.Any(snapshot => snapshot.material == material);
        }

        public static int CountFor(IEnumerable<Material> materials)
        {
            return materials == null ? 0 : materials.Where(material => material != null).Distinct().Count(Contains);
        }

        public static void Capture(Material material)
        {
            if(material == null || Contains(material)) return;

            var snapshot = new Snapshot
            {
                material = material,
                json = EditorJsonUtility.ToJson(material)
            };
            var serialized = new SerializedObject(material);
            var property = serialized.GetIterator();
            while(property.Next(true))
            {
                if(property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                    continue;
                snapshot.references.Add(new ObjectReference
                {
                    propertyPath = property.propertyPath,
                    value = property.objectReferenceValue
                });
            }

            Undo.RegisterCompleteObjectUndo(instance, "Start Material Debug");
            instance.snapshots.Add(snapshot);
            instance.Save(true);
        }

        public static void Restore(IEnumerable<Material> materials)
        {
            var selected = SelectSnapshots(materials);
            if(selected.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore Material Before Debug");
            Undo.RegisterCompleteObjectUndo(
                selected.Select(snapshot => (Object)snapshot.material).Concat(new Object[] { instance }).ToArray(),
                "Restore Material Before Debug");
            try
            {
                foreach(var snapshot in selected)
                {
                    // EditorJsonUtility stores persistent object references by GUID and file ID.
                    // The serialized reference table also retains non-asset objects across a domain reload.
                    EditorJsonUtility.FromJsonOverwrite(snapshot.json, snapshot.material);
                    var serialized = new SerializedObject(snapshot.material);
                    foreach(var reference in snapshot.references)
                    {
                        if(reference.value == null) continue;
                        var property = serialized.FindProperty(reference.propertyPath);
                        if(property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                            property.objectReferenceValue = reference.value;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(snapshot.material);
                    instance.snapshots.Remove(snapshot);
                }
            }
            finally
            {
                instance.Save(true);
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        // Keep the current material appearance and release only the selected diagnostic backups.
        public static void Forget(IEnumerable<Material> materials)
        {
            var selected = SelectSnapshots(materials);
            if(selected.Count == 0) return;
            Undo.RegisterCompleteObjectUndo(instance, "Keep Debug Material");
            foreach(var snapshot in selected) instance.snapshots.Remove(snapshot);
            instance.Save(true);
        }

        private static List<Snapshot> SelectSnapshots(IEnumerable<Material> materials)
        {
            if(materials == null) return new List<Snapshot>();
            var selected = new HashSet<Material>(materials.Where(material => material != null));
            return instance.snapshots.Where(snapshot => snapshot.material != null && selected.Contains(snapshot.material)).ToList();
        }

        private static void SaveAfterUndo()
        {
            instance.Save(true);
        }
    }
}
#endif
