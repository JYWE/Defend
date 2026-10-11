using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Defend.Unity;
using UnityEngine;

public static class PhaseThreeCleanupValidation
{
    public static object Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("请先进入播放模式。");
        var session = UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();
        session.enabled = false;
        var world = UnityEngine.Object.FindAnyObjectByType<WorldPresentation>();
        var battle = UnityEngine.Object.FindAnyObjectByType<BattlePresentation>();
        var results = new List<string>();
        CheckPool(Field(world, "transferPool"), "能源表现", results);
        foreach (DictionaryEntry entry in (IDictionary)Field(world, "buildingPools"))
            CheckPool(entry.Value, "建筑 " + entry.Key, results);
        CheckPool(Field(battle, "enemyPool"), "敌人表现", results);
        CheckPool(Field(battle, "shotPool"), "射击表现", results);
        return new { passed = true, poolCount = results.Count, results };
    }

    private static object Field(object owner, string name) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    private static void CheckPool(object pool, string name, List<string> results)
    {
        Type type = pool.GetType();
        var item = (Component)type.GetMethod("Get", Type.EmptyTypes).Invoke(pool, null);
        type.GetMethod("Release").Invoke(pool, new object[] { item });
        // 复现关闭程序时子物体先于表现管理器销毁的顺序。
        UnityEngine.Object.DestroyImmediate(item.gameObject);
        type.GetMethod("Clear").Invoke(pool, null);
        results.Add(name + "：已销毁的池对象清理通过");
    }
}
