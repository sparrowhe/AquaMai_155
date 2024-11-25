using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.UserDatas;
using MelonLoader;
using Net;
using Net.Packet;
using Net.Packet.Mai2;
using Net.VO;

namespace AquaMai.Core.Helpers;

public static class Shim
{
    private static T Iife<T>(Func<T> func) => func();

    public static readonly string apiSuffix = Iife(() =>
    {
        try
        {
            var baseNetQueryConstructor = typeof(NetQuery<VOSerializer, VOSerializer>)
                .GetConstructors()
                .First();
            return ((INetQuery)baseNetQueryConstructor.Invoke(
                baseNetQueryConstructor
                    .GetParameters()
                    .Select((parameter, i) => i == 0 ? "" : parameter.DefaultValue)
                    .ToArray())).Api;
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Failed to resolve the API suffix: {e}");
            return null;
        }
    });

    public static string RemoveApiSuffix(string api)
    {
        return !string.IsNullOrEmpty(apiSuffix) && api.EndsWith(apiSuffix)
            ? api.Substring(0, api.Length - apiSuffix.Length)
            : api;
    }

    public delegate string GetAccessTokenMethod(int index);
    public static readonly GetAccessTokenMethod GetAccessToken = Iife<GetAccessTokenMethod>(() =>
    {
        var tOperationManager = Traverse.Create(Singleton<OperationManager>.Instance);
        var tGetAccessToken = tOperationManager.Method("GetAccessToken", [typeof(int)]);
        if (!tGetAccessToken.MethodExists())
        {
            return (index) => throw new MissingMethodException("No matching OperationManager.GetAccessToken() method found");
        }
        return (index) => tGetAccessToken.GetValue<string>(index);
    });

    public delegate PacketUploadUserPlaylog PacketUploadUserPlaylogCreator(int index, UserData src, int trackNo, Action<int> onDone, Action<PacketStatus> onError = null);
    public static readonly PacketUploadUserPlaylogCreator CreatePacketUploadUserPlaylog = Iife<PacketUploadUserPlaylogCreator>(() =>
    {
        var type = typeof(PacketUploadUserPlaylog);
        if (type.GetConstructor([typeof(int), typeof(UserData), typeof(int), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor1)
        {
            return (index, src, trackNo, onDone, onError) =>
            {
                var args = new object[] { index, src, trackNo, onDone, onError };
                return (PacketUploadUserPlaylog)ctor1.Invoke(args);
            };
        }
        else if (type.GetConstructor([typeof(int), typeof(UserData), typeof(int), typeof(string), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor2)
        {
            return (index, src, trackNo, onDone, onError) =>
            {
                var accessToken = GetAccessToken(index);
                var args = new object[] { index, src, trackNo, accessToken, onDone, onError };
                return (PacketUploadUserPlaylog)ctor2.Invoke(args);
            };
        }
        else
        {
            throw new MissingMethodException("No matching PacketUploadUserPlaylog constructor found");
        }
    });

    public delegate PacketUpsertUserAll PacketUpsertUserAllCreator(int index, UserData src, Action<int> onDone, Action<PacketStatus> onError = null);
    public static readonly PacketUpsertUserAllCreator CreatePacketUpsertUserAll = Iife<PacketUpsertUserAllCreator>(() =>
    {
        var type = typeof(PacketUpsertUserAll);
        if (type.GetConstructor([typeof(int), typeof(UserData), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor1)
        {
            return (index, src, onDone, onError) =>
            {
                var args = new object[] { index, src, onDone, onError };
                return (PacketUpsertUserAll)ctor1.Invoke(args);
            };
        }
        else if (type.GetConstructor([typeof(int), typeof(UserData), typeof(string), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor2)
        {
            return (index, src, onDone, onError) =>
            {
                var accessToken = GetAccessToken(index);
                var args = new object[] { index, src, accessToken, onDone, onError };
                return (PacketUpsertUserAll)ctor2.Invoke(args);
            };
        }
        else
        {
            throw new MissingMethodException("No matching PacketUpsertUserAll constructor found");
        }
    });

    public static IEnumerable<UserScore>[] GetUserScoreList(UserData userData)
    {
        var tUserData = Traverse.Create(userData);

        var tScoreList = tUserData.Property("ScoreList");
        if (tScoreList.PropertyExists())
        {
            return tScoreList.GetValue<List<UserScore>[]>();
        }

        var tScoreDic = tUserData.Property("ScoreDic");
        if (tScoreDic.PropertyExists())
        {
            var scoreDic = tScoreDic.GetValue<Dictionary<int, UserScore>[]>();
            return scoreDic.Select(dic => dic.Values).ToArray();
        }

        throw new MissingFieldException("No matching UserData.ScoreList/ScoreDic found");
    }
}
