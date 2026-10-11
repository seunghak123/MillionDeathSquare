using System;

[System.Serializable]
public enum CardRarity
{
	E_U,
	E_R,
	E_SR,
}
[System.Serializable]
public enum CardPropertyType
{
	E_VOCAL,
	E_DANCE,
	E_RAP,
	E_ALLURE,
}
[System.Serializable]
public enum ActivationTime
{
	E_WHEN_SUMMON,
	E_WHEN_EXIT,
	E_WHEN_DESTROY_SIREN_CARD,
	E_WHEN_BREAKDOWN,
}
[System.Serializable]
public enum SkillType
{
	E_CAN_ATTACK_ALL_STAGE,
	E_DRAW,
	E_INCREASE_STATIC,
	E_INCREASE_VARIABLE,
	E_DECREASE_STATIC,
	E_CHANGE_CONDITION,
	E_ABSORB,
	E_DESTROY,
	E_MOVE,
	E_CHANGE,
	E_END_TURN,
	E_CANT_USE_ARTS,
	E_CHANGE_LV,
	E_ADD,
	E_DECREASE_VARIABLE,
}
[System.Serializable]
public enum SkillSource
{
	E_NONE,
	E_ATK,
	E_LIFE,
	E_BD,
	E_SLIP,
	E_AP,
	E_AP_RECOVERY,
	E_RANDOM_CARD,
	E_STANDBY,
	E_RANDOM_SIREN_CARD,
	E_PARROT,
	E_CROWN_PARROT,
	E_ALL_ENEMY_SIRENS,
	E_CRITICAL_RATE,
	E_LV,
	E_ALL_HAND_CARD,
}
[System.Serializable]
public enum EffectValueCalculation
{
	E_NONE,
	E_MULTIPLY_MY_VOCAL,
	E_MULTIPLY_MY_DANCER,
	E_MULTIPLY_MY_RAP,
	E_MULTIPLY_MY_ALLURE,
	E_MULTIPLY,
}
[System.Serializable]
public enum TargetType
{
	DestinationTargetType,
	E_NONE,
	E_SELF,
	E_LEADER,
	E_ENEMY_LEADER,
	E_MY_HAND,
	E_AMBIENT_SIREN,
	E_SINGLE_MY_SIREN,
	E_SINGLE_ENEMY_SIREN,
	E_ALL_ENEMY_SIRENS,
	E_MY_SHELTER,
	E_ENEMY_SHELTER,
	E_ALL_MY_SIRENS,
	E_STANDBY,
}
