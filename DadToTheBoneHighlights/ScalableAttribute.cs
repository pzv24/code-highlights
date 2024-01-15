using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace Dads.Abilities
{
    // modular value type, can be used as a float and then modified by equipping various "buffs"
    [Serializable]
    public class ScalableAttribute
    {
        public ScalableAttributesEnum _attributeEnum;
        [OnValueChanged("CalculateValue")]
        public float BaseValue;
        [ReadOnly, OnValueChanged("CalculateValue")]
        public float ModifierValue;
        [ReadOnly]
        public float ActualValue;

        [ReadOnly]
        public int StacksApplieddd = 0;
        public float MaxValue;

        public float MultiplierForShownValue = 1;
        public bool RoundUpShowingValue = false;
        
        private bool _isDebugEnabled = true;

        [ReadOnly]
        public ScalableAttributesEnum AttributesEnum { get { return _attributeEnum; } }

        [ReadOnly, OnCollectionChanged("CalculateValue")]
        [SerializeField] private List<Buff> _appliedBuffs;
        public List<Buff> AppliedBuffs { get { return _appliedBuffs;  } }
        public ScalableAttribute(ScalableAttributesEnum attribute, float baseValue)
        {
            _attributeEnum = attribute;
            _appliedBuffs = new List<Buff>();

            BaseValue = baseValue;
            ModifierValue = 0;
            ActualValue = BaseValue;
        }
        // the actual value used in calculations
        public float CalculateValue()
        {
            ActualValue = ApplyBuffs();
            return ActualValue;
        }

        // the number to display in UI, as we don't want any decimals
        public float ReturnPlayerReadableValue()
        {
            float value = CalculateValue();
            value = value * MultiplierForShownValue;
            value = Mathf.Round(value * 10) / 10;
            if (RoundUpShowingValue)
            {
                return Mathf.RoundToInt(value);
            }
            else
            {
                return value;
            }

        }
        public float ReturnNumberWithReadabilityRules(float value)
        {
            value = value * MultiplierForShownValue;
            value = Mathf.Round(value * 10) / 10;
            if (RoundUpShowingValue)
            {
                return Mathf.RoundToInt(value);
            }
            else
            {
                return value;
            }
        }
        // apply current equipped buffs to attribute
        private float ApplyBuffs()
        {
            ModifierValue = 0;
            foreach(Buff buff in _appliedBuffs)
            {
                ModifierValue += buff.CalculateModifier(BaseValue);
            }
            return BaseValue + ModifierValue;
        }
        // add new buff to buff list, 
        public void AddBuff(Buff buff)
        {
            //checks if it is a instance of very same stack
            if (buff.IsUnique)
            {
                bool alreadyBuffed = _appliedBuffs.Exists((x) => x.Title.Equals(buff.Title));
                if (alreadyBuffed) return;
            }
            
            Buff existingBuff = _appliedBuffs.Find((x) => x.BuffAttribute.HasFlag(buff.BuffAttribute));

            // increase the stack value if the buff already exists, add the buff as a new one if not
            if (existingBuff != null)
            {
                if(_isDebugEnabled) Debug.Log("A buff with the same effect has been found, increasing the stacks instead");
                existingBuff.IncreaseStacks();
                StacksApplieddd = existingBuff.StacksApplied;
            }
            else
            {
                Buff buffCopy = Buff.Instantiate(buff);
                _appliedBuffs.Add(buffCopy);
                StacksApplieddd = buffCopy.StacksApplied;
            }
            CalculateValue();
        }

        public void RemoveBuff(Buff buff)
        {
            Buff buffToRemove = _appliedBuffs.Find((x) => x.BuffAttribute.HasFlag(buff.BuffAttribute));
            if(buffToRemove == null)
            {
                Debug.LogError("No buff of type: " + buff.BuffAttribute.ToString() + " has been found");
                return;
            }


            if(buffToRemove.StacksApplied > 0)
            {
                buffToRemove.DecreaseStacks();
                StacksApplieddd = buffToRemove.StacksApplied;
                return;
            }
            else
            {
                StacksApplieddd = 0;
                Buff.Destroy(buffToRemove);
            }
        }

        public void ClearBuffs()
        {
            if(_appliedBuffs.Count > 0)
            {
                _appliedBuffs.Clear();
            }
            CalculateValue();
        }

        public void SetBaseValue(float value)
        {
            BaseValue= value;
        }
    }
}

