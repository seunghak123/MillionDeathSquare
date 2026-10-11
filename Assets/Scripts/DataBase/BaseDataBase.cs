using Seunghak.Common;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface BaseDataBase 
{
    public bool InitDB();
    public void SaveDB(UserDataInfo saveData);
    public void UpdateDB(UserDataInfo updateData);
    public UserDataInfo LoadDB();
}
