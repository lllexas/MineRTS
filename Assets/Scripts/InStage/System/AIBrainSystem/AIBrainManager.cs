using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AIBrain
{
    public class AIBrainManager : SingletonMono<AIBrainManager>
    {
        // 存储原型实例的字典 (原型库)
        // Key: 标识符 (e.g., "human_offensive_v1")
        // Value: 该 AI 行为的一个原型实例
        private readonly Dictionary<string, AIBrainBar> _prototypes = new Dictionary<string, AIBrainBar>();

        // 重写 Singleton 的 Awake 方法以执行注册逻辑
        protected override void Awake()
        {
            base.Awake(); // 调用基类的 Awake
            RegisterAllAIBrains();
        }

        /// <summary>
        /// 使用反射扫描当前程序集，查找所有标记了 [AIBrainBarHere] 的类，
        /// 并创建它们的实例作为原型存入字典。
        /// </summary>
        private void RegisterAllAIBrains()
        {
            // 获取当前正在执行的代码所在的程序集
            var assembly = Assembly.GetExecutingAssembly();

            // 查找所有继承自 AIBrainBar 的非抽象类
            var brainTypes = assembly.GetTypes().Where(t =>
                t.IsSubclassOf(typeof(AIBrainBar)) &&
                !t.IsAbstract
            );

            foreach (var type in brainTypes)
            {
                // 获取类上定义的 AIBrainBarHere 特性
                var attribute = type.GetCustomAttribute<AIBrainBarHere>();
                if (attribute != null)
                {
                    // 获取标识符
                    string identifier = attribute.Identifier;

                    if (_prototypes.ContainsKey(identifier))
                    {
                        Debug.LogWarning($"AIBrainManager: 发现重复的AI标识符 '{identifier}'，类型为 {type.Name}。旧的将被覆盖。");
                    }

                    try
                    {
                        // 创建该类型的一个实例作为原型
                        var prototypeInstance = Activator.CreateInstance(type) as AIBrainBar;
                        if (prototypeInstance != null)
                        {
                            // 将原型存入字典
                            _prototypes[identifier] = prototypeInstance;
                            Debug.Log($"AIBrainManager: 成功注册AI原型 -> '{identifier}' ({type.Name})");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"AIBrainManager: 创建AI原型 '{identifier}' ({type.Name}) 失败: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 从原型库中获取一个指定标识符的AI Brain的克隆实例。
        /// </summary>
        /// <param name="identifier">AI行为的唯一标识符</param>
        /// <param name="teamId">要分配给这个新AI实例的阵营ID</param>
        /// <returns>一个新的AIBrainBar实例，如果找不到原型则返回null</returns>
        public AIBrainBar GetBrainClone(string identifier, int teamId)
        {
            if (_prototypes.TryGetValue(identifier, out var prototype))
            {
                try
                {
                    // 从原型克隆一个新的实例
                    var newBrainInstance = prototype.Clone() as AIBrainBar;
                    if (newBrainInstance != null)
                    {
                        // 对新实例进行初始化
                        newBrainInstance.Initialize(teamId, identifier);
                        return newBrainInstance;
                    }
                    return null;
                }
                catch (Exception e)
                {
                    Debug.LogError($"AIBrainManager: 克隆AI原型 '{identifier}' 失败: {e.Message}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"AIBrainManager: 无法找到标识符为 '{identifier}' 的AI原型。");
                return null;
            }
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AIBrainBarHere : System.Attribute
    {
        public string Identifier { get; }

        public AIBrainBarHere(string identifier)
        {
            this.Identifier = identifier;
        }
    }
}