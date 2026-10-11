using Seunghak;
using Seunghak.Common;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StringManager : UnitySingleton<StringManager>
{
    public static string GetString(string stringKey)
    {
        //JStringTableData data = JsonDataManager.Instance.GetSingleData<JStringTableData>(stringKey, E_JSON_TYPE.JStringTableData);

        ////국가에 따라서 return
        //if(data == null)
        //{
        //    return stringKey;
        //}

        ////국가 값 가져와서 세팅해줄것

        //return data.KOR;
        return stringKey;
    }
}
