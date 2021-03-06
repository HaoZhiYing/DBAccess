﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//
using DBAccess.Entity;
using DBAccess.CustomAttribute;
using DBAccess.SQLContext;
using DBAccess.HelperClass;
using DBAccess.AdoDotNet;
using System.Reflection;

namespace DBAccess.CheckClass
{
    /// <summary>
    /// 验证
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CheckContext<T> where T : BaseModel, new()
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        DBHelper dbhelper;

        DBType _DBType;

        private CheckContext() { }

        private string _ConnectionString { get; set; }

        public CheckContext(string ConnectionString, DBType DBType)
        {
            _ConnectionString = ConnectionString;
            dbhelper = new DBHelper(_ConnectionString, DBType);
            _DBType = DBType;
        }

        public bool Check(T entity)
        {
            var list = entity.EH.GetAllPropertyInfo(entity);
            list = list.FindAll(item => entity.fileds.ContainsKey(item.Name));
            list = list.FindAll(item => !entity.NotChecks.Contains(item.Name));

            foreach (var item in list)
            {
                if (!this.Main(item, entity))
                    return false;
            }
            return true;
        }

        public bool Main(PropertyInfo item, T entity)
        {
            var DisplayName = entity.EH.GetDisplayName(entity, item.Name);
            var Value = item.GetValue(entity, null);
            if (!CRequired(item, entity, DisplayName, Value))
                return false;
            if (!CStringLength(item, entity, DisplayName, Value))
                return false;
            if (!CRegularExpression(item, entity, DisplayName, Value))
                return false;
            if (!CCompare(item, entity, DisplayName, Value))
                return false;
            if (!CRepeat(item, entity, DisplayName, Value))
                return false;
            if (!SetNumber(item, entity, DisplayName, Value))
                return false;
            return true;
        }

        /// <summary>
        /// 非空验证
        /// </summary>
        /// <param name="item"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool CRequired(PropertyInfo item, T entity, string DisplayName, object Value)
        {
            var fileName = item.Name;
            var tag = entity.EH.GetAttrTag<CRequiredAttribute>(entity, fileName);
            if (tag != null)
            {
                if (Value == null)
                {
                    SetErrorMessage(tag.ErrorMessage, DisplayName + "不能为空", DisplayName);
                    return false;
                }
                else
                {
                    if (item.PropertyType.Equals(typeof(string)))
                    {
                        if (string.IsNullOrEmpty(Value.ToString()))
                        {
                            SetErrorMessage(tag.ErrorMessage, DisplayName + "不能为空", DisplayName);
                            return false;
                        }
                    }
                    else if (item.PropertyType.Equals(typeof(Guid?)))
                    {
                        if (Guid.Parse(Value.ToString()).Equals(Guid.Empty))
                        {
                            SetErrorMessage(tag.ErrorMessage, DisplayName + "不能为空", DisplayName);
                            return false;
                        }
                    }
                    //throw new AggregateException("类型 " + item.PropertyType + " 不支持验证!");
                }
            }
            return true;
        }

        /// <summary>
        /// 字符串长度验证
        /// </summary>
        /// <param name="item"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool CStringLength(PropertyInfo item, T entity, string DisplayName, object Value)
        {
            if (Value != null)
            {
                //获取有特性标记的属性【字符串长度验证】
                var fileName = item.Name;
                var sign = entity.EH.GetAttrTag<CStringLengthAttribute>(entity, fileName);

                if (sign != null && (Value.ToString().Length < sign.MinLength || Value.ToString().Length > sign.MaxLength))
                {
                    SetErrorMessage(sign.ErrorMessage, DisplayName + "长度介于" + sign.MinLength + "-" + sign.MaxLength + "之间", DisplayName);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 正则表达式验证
        /// </summary>
        /// <param name="item"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool CRegularExpression(PropertyInfo item, T entity, string DisplayName, object Value)
        {
            if (Value != null)
            {
                //获取有特性标记的属性【正则表达式验证】
                var fileName = item.Name;
                var sign = entity.EH.GetAttrTag<CRegularExpressionAttribute>(entity, fileName);
                if (sign != null && !System.Text.RegularExpressions.Regex.IsMatch(Value.ToString(), sign.Pattern))
                {
                    SetErrorMessage(sign.ErrorMessage, DisplayName + "格式不正确", DisplayName);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 比较两字段值是否相同
        /// </summary>
        /// <param name="item"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool CCompare(PropertyInfo item, T entity, string DisplayName, object Value)
        {
            if (Value != null)
            {
                //获取有特性标记的属性【比较两字段值是否相同】
                var fileName = item.Name;
                var sign = entity.EH.GetAttrTag<CCompareAttribute>(entity, fileName);
                if (sign != null)
                {
                    var list = entity.EH.GetAllPropertyInfo(entity);
                    foreach (var info in list)
                    {
                        var infoname = entity.EH.GetAttrTag<CCompareAttribute>(entity, fileName);
                        if (info.Name.Equals(sign.OtherProperty) && !info.GetValue(entity, null).Equals(Value))
                        {
                            SetErrorMessage(sign.ErrorMessage, DisplayName + "的值与" + infoname + "不匹配", DisplayName);
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 验证数据是否重复
        /// </summary>
        /// <param name="item"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool CRepeat(PropertyInfo item, T entity, string DisplayName, object Value)
        {
            if (Value != null)
            {
                string TableName = entity.TableName;
                //获取有特性标记的属性【非空】
                var fileName = item.Name;
                var sign = entity.EH.GetAttrTag<CRepeatAttribute>(entity, fileName);
                if (sign != null)
                {
                    //取ID的值
                    string KeyValue = string.Empty, KeyName = string.Empty;
                    var list = entity.EH.GetAllPropertyInfo(entity);
                    KeyValue = entity.EH.GetKeyValue(entity);
                    KeyName = entity.EH.GetKeyName(entity);
                    string where = string.Empty;
                    if (!string.IsNullOrEmpty(KeyValue) && !KeyValue.ToString().Equals(Guid.Empty.ToString()))
                        where = " AND " + KeyName + "<>'" + KeyValue + "'";

                    //判断条件，是否存在  || 自定义条件 语法  ：and filed1='{filed1}' ||
                    if (!string.IsNullOrEmpty(sign.Where))
                    {
                        foreach (var pi in list)
                        {
                            if (sign.Where.Contains("{" + pi.Name + "}"))
                            {
                                where += sign.Where + " ";
                                where = where.Replace("{" + pi.Name + "}", pi.GetValue(entity, null) == null ? "" : pi.GetValue(entity, null).ToString());
                            }
                        }
                    }

                    string sql = "SELECT COUNT(1) FROM " + TableName + " WHERE 1=1 AND " + fileName + "='" + Value + "' " + where;
                    if (Tool.ToInt(dbhelper.ExecuteScalar(sql)) > 0)
                    {
                        SetErrorMessage(sign.ErrorMessage, DisplayName + "已存在", DisplayName);
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 设置编号
        /// </summary>
        /// <param name="item"></param>
        /// <param name="Model"></param>
        /// <returns></returns>
        public bool SetNumber(PropertyInfo item, T Model, string DisplayName, object Value)
        {
            string TableName = Model.TableName;
            //获取有特性标记的属性【编号】
            var sign = Model.EH.GetAttrTag<CSetNumberAttribute>(Model, item.Name);
            if (sign != null)
            {
                //取ID的值
                string KeyValue = string.Empty;
                var list = Model.EH.GetAllPropertyInfo(Model);
                KeyValue = Model.EH.GetKeyValue(Model);
                if (string.IsNullOrEmpty(KeyValue) || KeyValue.ToString().Equals(Guid.Empty.ToString()))
                {
                    var sql = string.Empty;
                    switch (_DBType)
                    {
                        case DBType.SqlServer: sql = " exec getnumber '" + item.Name + "','" + TableName + "'";
                            break;
                        case DBType.MySql: sql = " call getnumber ('" + item.Name + "','" + TableName + "') ";
                            break;
                        default: sql = " exec getnumber '" + item.Name + "','" + TableName + "'";
                            break;
                    }
                    var dt = dbhelper.ExecuteDataset(sql);
                    var num = dt.Rows[0][0];
                    if (num == null)
                        throw new AggregateException("设置编号错误：数据无法查出！");
                    if (item.PropertyType == typeof(int))
                        item.SetValue(Model, Tool.ToInt(num.ToString().PadLeft(sign.Length, sign.Str)), null);
                    else
                        item.SetValue(Model, Tool.ToString(num).PadLeft(sign.Length, sign.Str), null);
                }
            }
            return true;
        }

        /// <summary>
        /// 设置错误消息
        /// </summary>
        /// <param name="error1">开发者自己设定的错误</param>
        /// <param name="error2">程序设定的错误</param>
        /// <param name="name">字段的中文名称</param>
        public void SetErrorMessage(string error1, string error2, string name)
        {
            if (string.IsNullOrEmpty(error1))
                this.ErrorMessage = error2;
            else
            {
                if (error1.Contains("{name}"))
                    this.ErrorMessage = error1.Replace("{name}", name);
                else
                    this.ErrorMessage = error1;
            }
        }


    }
}
