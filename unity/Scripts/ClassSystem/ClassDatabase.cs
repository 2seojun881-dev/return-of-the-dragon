using System.Collections.Generic;
using UnityEngine;

namespace Game.ClassSystem
{
    /// <summary>
    /// ClassId → ClassData 조회표. 네트워크로는 ClassData 에셋 대신 ClassId 문자열만 보내고,
    /// 서버와 각 클라이언트가 이 표에서 같은 에셋을 찾아 씁니다 (에셋 참조가 스레드/연결 너머로 사라지는 문제 방지).
    /// Resources 폴더에 "ClassDatabase" 이름으로 두면 자동으로 찾습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ClassDatabase", menuName = "Character/Class Database", order = 3)]
    public class ClassDatabase : ScriptableObject
    {
        public List<ClassData> Classes = new List<ClassData>();

        private static ClassDatabase instance;
        public static ClassDatabase Instance => instance != null ? instance : (instance = Resources.Load<ClassDatabase>("ClassDatabase"));

        public ClassData Find(string classId)
        {
            foreach (var c in Classes) if (c != null && c.ClassId == classId) return c;
            return null;
        }

        /// <summary>이 직업의 각성 직업 (없으면 null)</summary>
        public ClassData FindAwakeningOf(ClassData baseClass)
        {
            foreach (var c in Classes) if (c != null && c.parentClassData == baseClass) return c;
            return null;
        }
    }
}
