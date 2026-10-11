using Seunghak.Common;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocalDataBase : BaseDataBase
{
    public bool InitDB()
    {
        return true;
    }
    public UserDataInfo LoadDB()
    {
        UserDataInfo userInfo = new UserDataInfo();

        //유저 데이터 Info
        userInfo.userInfoData = CommonUtil.GetPlayerPref<UserInfoData>(PlayerPrefKey.UserInfoData);

        userInfo.userOption = CommonUtil.GetPlayerPref<UserOptionData>(PlayerPrefKey.UserOptionData);

        userInfo.userItemDatas = CommonUtil.GetPlayerPref<UserItemDatas>(PlayerPrefKey.UserItemDatas);

        userInfo.userStoryDatas = CommonUtil.GetPlayerPref<UserStoryDatas>(PlayerPrefKey.UserStoryDatas);

        userInfo.userLobbyInfoDatas = CommonUtil.GetPlayerPref<UserLobbyInfoData>(PlayerPrefKey.UserLobbyDatas);

        return userInfo;
    }
    public void SaveDB(UserDataInfo saveData)
    {
        CommonUtil.SavePlayerPref<UserInfoData>(PlayerPrefKey.UserInfoData, saveData.userInfoData);

        CommonUtil.SavePlayerPref<UserOptionData>(PlayerPrefKey.UserOptionData, saveData.userOption);

        CommonUtil.SavePlayerPref<UserItemDatas>(PlayerPrefKey.UserItemDatas, saveData.userItemDatas);

        CommonUtil.SavePlayerPref<UserStoryDatas>(PlayerPrefKey.UserStoryDatas, saveData.userStoryDatas);

        CommonUtil.SavePlayerPref<UserLobbyInfoData>(PlayerPrefKey.UserLobbyDatas, saveData.userLobbyInfoDatas);
    }
    public void UpdateDB(UserDataInfo updateData)
    {
        CommonUtil.SavePlayerPref<UserInfoData>(PlayerPrefKey.UserInfoData, updateData.userInfoData);

        CommonUtil.SavePlayerPref<UserOptionData>(PlayerPrefKey.UserOptionData, updateData.userOption);

        CommonUtil.SavePlayerPref<UserItemDatas>(PlayerPrefKey.UserItemDatas, updateData.userItemDatas);

        CommonUtil.SavePlayerPref<UserStoryDatas>(PlayerPrefKey.UserStoryDatas, updateData.userStoryDatas);

        CommonUtil.SavePlayerPref<UserLobbyInfoData>(PlayerPrefKey.UserLobbyDatas, updateData.userLobbyInfoDatas);
    }
}
