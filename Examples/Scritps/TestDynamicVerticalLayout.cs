using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

//系统消息类型的item
//viewType类型为0
public class SystemInfoItemView : IFlexibleItemView
{
    public RectTransform rectTransform { get; set; }

    public int ViewType
    {
        get { return 0; }
    }

    //显示消息的文本
    public Text msgText;
}

//他人文本消息类型的item
//viewType类型为1
public class OtherTextItemView : IFlexibleItemView
{
    public RectTransform rectTransform { get; set; }

    public int ViewType
    {
        get { return 1; }
    }

    //显示消息的文本
    public Text msgText;
}

//他人语音消息类型的item
//viewType类型为2
public class OtherVoiceItemView : IFlexibleItemView
{
    public RectTransform rectTransform { get; set; }

    public int ViewType
    {
        get { return 2; }
    }

    //显示消息的文本
    public Text msgText;
}

//自己文本消息类型的item
//viewType类型为3
public class MyselfTextItemView : IFlexibleItemView
{
    public RectTransform rectTransform { get; set; }

    public int ViewType
    {
        get { return 3; }
    }

    //显示消息的文本
    public Text msgText;
}

//自己语言消息类型的item
//viewType类型为4
public class MyselfVoiceItemView : IFlexibleItemView
{
    public RectTransform rectTransform { get; set; }

    public int ViewType
    {
        get { return 4; }
    }

    //显示消息的文本
    public Text msgText;
}

public enum MessageType
{
    SystemInfo,
    OtherTextInfo,
    OtherVocieInfo,
    MyselfTextInfo,
    MyselfVoiceInfo,
}

public class MessageInfo
{
    public MessageType messageType;
    public string msgText;
}

public class TestDynamicVerticalLayout : MonoBehaviour, IFlexibleAdapter
{
    protected List<MessageInfo> m_DataInfoList;
    protected List<MessageInfo> m_Channel1MsgList = new List<MessageInfo>();
    protected List<MessageInfo> m_Channel2MsgList = new List<MessageInfo>();

    protected List<MessageInfo> m_TmpMsgList = new List<MessageInfo>();

    public GameObject systemInfoGo;
    public GameObject otherTextInfoGo;
    public GameObject otherVoiceInfoGo;
    public GameObject myTextInfoGo;
    public GameObject myVoiceInfoGo;

    public DynamicFlexibleLayout dynamicLayout;

    public GameObject headGo;
    public Text headText;

    void Start()
    {
        systemInfoGo.transform.localPosition = new Vector3(0,0,-100000);
        otherTextInfoGo.transform.localPosition = new Vector3(0, 0, -100000);
        otherVoiceInfoGo.transform.localPosition = new Vector3(0, 0, -100000);
        myTextInfoGo.transform.localPosition = new Vector3(0, 0, -100000);
        myVoiceInfoGo.transform.localPosition = new Vector3(0, 0, -100000);

        m_DataInfoList = new List<MessageInfo>();
        for (int i = 0; i < 10; i++)
        {
            var messageInfo = new MessageInfo();
            System.Random r = new System.Random(Environment.TickCount + i);
            messageInfo.messageType = (MessageType) (r.Next(0, 5));

            var sb = new StringBuilder();
            sb.AppendLine(string.Format((messageInfo.messageType == MessageType.SystemInfo ? "          " : "") + "这是第{0}条数据", i + 1));

            System.Random r1 = new System.Random(Environment.TickCount + i + 1);
            var row = r1.Next(1, 6);
            for (int j = 0; j < row; j++)
            {
                System.Random r2 = new System.Random(Environment.TickCount + i + 2 + j);
                var charCount = r2.Next(1, 20);
                for (int k = 0; k < charCount; k++)
                {
                    sb.Append('a' + k);
                }
                sb.AppendLine();
            }
            sb.Remove(sb.Length - 1, 1);
            messageInfo.msgText = sb.ToString();

            m_Channel1MsgList.Add(messageInfo);
        }

        for (int i = 0; i < 1000; i++)
        {
            var messageInfo = new MessageInfo();
            System.Random r = new System.Random(Environment.TickCount + i);
            messageInfo.messageType = (MessageType)(r.Next(0, 5));

            var sb = new StringBuilder();
            sb.AppendLine(string.Format((messageInfo.messageType == MessageType.SystemInfo ? "          " : "") + "这是第{0}条数据", i + 1));

            System.Random r1 = new System.Random(Environment.TickCount + i + 1);
            var row = r1.Next(1, 6);
            for (int j = 0; j < row; j++)
            {
                System.Random r2 = new System.Random(Environment.TickCount + i + 2 + j);
                var charCount = r2.Next(1, 20);
                for (int k = 0; k < charCount; k++)
                {
                    sb.Append('a' + k);
                }
                sb.AppendLine();
            }
            sb.Remove(sb.Length - 1, 1);
            messageInfo.msgText = sb.ToString();

            m_Channel2MsgList.Add(messageInfo);
        }

        dynamicLayout.canLockEvent.AddListener((islock)=>
        {
            if (islock)
            {
                if (m_TmpMsgList.Count > 0)
                {
                    headGo.SetActive(true);
                    ShowText();
                    return;
                }
            }

            if (m_TmpMsgList.Count > 0)
            {
                m_DataInfoList.AddRange(m_TmpMsgList);
                m_TmpMsgList.Clear();
                dynamicLayout.RefreshAllItem();
            }
            headGo.SetActive(false);       
        });

        /*dynamicLayout.loadMoreEvent.AddListener(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                var messageInfo = new MessageInfo();
                System.Random r = new System.Random(Environment.TickCount + i);
                messageInfo.messageType = (MessageType)(r.Next(0, 5));

                var sb = new StringBuilder();
                sb.AppendLine(string.Format((messageInfo.messageType == MessageType.SystemInfo ? "          " : "") + "这是第{0}条数据", m_DataInfoList.Count + m_TmpMsgList.Count + 1));

                System.Random r1 = new System.Random(Environment.TickCount + 1 + i);
                var row = r1.Next(1, 6);
                for (int j = 0; j < row; j++)
                {
                    System.Random r2 = new System.Random(Environment.TickCount + 2 + j + i);
                    var charCount = r2.Next(1, 20);
                    for (int k = 0; k < charCount; k++)
                    {
                        sb.Append('a' + k);
                    }
                    sb.AppendLine();
                }
                sb.Remove(sb.Length - 1, 1);
                messageInfo.msgText = sb.ToString();
                m_DataInfoList.Add(messageInfo);
            }
            dynamicLayout.RefreshCurrentItem();
        });*/

        m_DataInfoList = m_Channel1MsgList;
        dynamicLayout.SetAdapter(this);

        StartCoroutine(GenerateMsg());
    }

    void ShowText()
    {
        headText.text = string.Format("未读消息{0}条", m_TmpMsgList.Count);
    }

    IEnumerator GenerateMsg()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(5);

            var messageInfo = new MessageInfo();
            System.Random r = new System.Random(Environment.TickCount);
            messageInfo.messageType = (MessageType)(r.Next(0, 5));

            var sb = new StringBuilder();
            sb.AppendLine(string.Format((messageInfo.messageType == MessageType.SystemInfo ? "          " : "") + "这是第{0}条数据", m_DataInfoList.Count + m_TmpMsgList.Count + 1));

            System.Random r1 = new System.Random(Environment.TickCount + 1);
            var row = r1.Next(1, 6);
            for (int j = 0; j < row; j++)
            {
                System.Random r2 = new System.Random(Environment.TickCount + 2 + j);
                var charCount = r2.Next(1, 20);
                for (int k = 0; k < charCount; k++)
                {
                    sb.Append('a' + k);
                }
                sb.AppendLine();
            }
            sb.Remove(sb.Length - 1, 1);
            messageInfo.msgText = sb.ToString();

            if (dynamicLayout.isLock)
            {
                m_TmpMsgList.Add(messageInfo);
                headGo.SetActive(true);
                ShowText();
            }
            else
            {
                m_DataInfoList.Add(messageInfo);
                dynamicLayout.RefreshAllItem();
            }
        }    
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (m_DataInfoList == m_Channel1MsgList)
            {
                m_DataInfoList = m_Channel2MsgList;
                dynamicLayout.RefreshAllItem();
            }
            else if (m_DataInfoList == m_Channel2MsgList)
            {
                m_DataInfoList = m_Channel1MsgList;
                dynamicLayout.RefreshAllItem();
            }      
        }
    }

    public int GetCount()
    {
        return m_DataInfoList.Count;
    }

    public bool IsEmpty()
    {
        return m_DataInfoList.Count == 0;
    }

    public IFlexibleItemView GetItemView(int position, RectTransform itemParent, DynamicFlexibleLayout parent)
    {
        //int index = position;
        int index = m_DataInfoList.Count - position - 1;
        switch (m_DataInfoList[index].messageType)
        {
            case MessageType.SystemInfo:
                var go = Instantiate(systemInfoGo, itemParent);
                var itemView = new SystemInfoItemView();
                itemView.rectTransform = go.transform as RectTransform; ;
                itemView.msgText = go.transform.GetChild(0).GetComponent<Text>();
                return itemView;
            case MessageType.OtherTextInfo:
                var go1 = Instantiate(otherTextInfoGo, itemParent);
                var itemView1 = new OtherTextItemView();
                itemView1.rectTransform = go1.transform as RectTransform; ;
                itemView1.msgText = go1.transform.Find("Info/Msg/Text").GetComponent<Text>();
                return itemView1;
            case MessageType.OtherVocieInfo:
                var go2 = Instantiate(otherVoiceInfoGo, itemParent);
                var itemView2 = new OtherVoiceItemView();
                itemView2.rectTransform = go2.transform as RectTransform; ;
                itemView2.msgText = go2.transform.Find("Info/Msg/Text").GetComponent<Text>();
                return itemView2;
            case MessageType.MyselfTextInfo:
                var go3 = Instantiate(myTextInfoGo, itemParent);
                var itemView3 = new MyselfTextItemView();
                itemView3.rectTransform = go3.transform as RectTransform; ;
                itemView3.msgText = go3.transform.Find("Info/Msg/Text").GetComponent<Text>();
                return itemView3;
            case MessageType.MyselfVoiceInfo:
                var go4 = Instantiate(myVoiceInfoGo, itemParent);
                var itemView4 = new MyselfVoiceItemView();
                itemView4.rectTransform = go4.transform as RectTransform; ;
                itemView4.msgText = go4.transform.Find("Info/Msg/Text").GetComponent<Text>();
                return itemView4;
        }

        return null;
    }

    public int GetItemViewType(int position)
    {
        //int index = position;
        int index = m_DataInfoList.Count - position - 1;
        return (int)m_DataInfoList[index].messageType;
    }

    //总共支持五种类型的item
    public int GetViewTypeCount()
    {
        return 5;
    }

    public void ProcessItemView(int position, IFlexibleItemView itemView, DynamicFlexibleLayout parent)
    {
        //第一条数据显示在最上面
        //var index = position;
        //最后一条数据显示在最上面
        var index = m_DataInfoList.Count - position - 1;
        switch (m_DataInfoList[index].messageType)
        {
            case MessageType.SystemInfo:
                var systemItemView = itemView as SystemInfoItemView;
                systemItemView.msgText.text = m_DataInfoList[index].msgText;
                break;
            case MessageType.OtherTextInfo:
                var otherTextItemView = itemView as OtherTextItemView;
                otherTextItemView.msgText.text = m_DataInfoList[index].msgText;
                break;
            case MessageType.OtherVocieInfo:
                var otherVoiceItemView = itemView as OtherVoiceItemView;
                otherVoiceItemView.msgText.text = m_DataInfoList[index].msgText;
                break;
            case MessageType.MyselfTextInfo:
                var myTextItemView = itemView as MyselfTextItemView;
                myTextItemView.msgText.text = m_DataInfoList[index].msgText;
                break;
            case MessageType.MyselfVoiceInfo:
                var myVoiceItemView = itemView as MyselfVoiceItemView;
                myVoiceItemView.msgText.text = m_DataInfoList[index].msgText;
                break;
            default:
                break;
        }
    }

    public bool RecycleItemView(IFlexibleItemView itemView, DynamicFlexibleLayout parent)
    {
        return false;
    }

    public void RecycleItemViewDone(DynamicFlexibleLayout parent)
    {
        
    }
}
