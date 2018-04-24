using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestVerticalPageLayout : MonoBehaviour, IFixedSizeItemAdapter
{
    protected List<int> m_DataList = new List<int>();
    protected List<int> m_TmpDataList = new List<int>();

    public VerticalPageLayout dynamicLayout;

    public RectTransform itemGo;
    public GameObject headGo;
    public Text headText;

    public int itemIndex;
    [Range(0, 1)]
    public float factor = 0;
    public bool useAnimation = true;

    void Start()
    {
        itemGo.localPosition = new Vector3(0,0,-100000);
        for (int i = 0; i < 10000; i++)
        {
            m_DataList.Add(m_DataList.Count);
        }

        dynamicLayout.canLockEvent.AddListener((islock)=>
        {
            if (islock)
            {
                if (m_TmpDataList.Count > 0)
                {
                    headGo.SetActive(true);
                    ShowText();
                    return;
                }
            }

            if (m_TmpDataList.Count > 0)
            {
                m_DataList.AddRange(m_TmpDataList);
                m_TmpDataList.Clear();
                dynamicLayout.RefreshAllItem();
            }
            headGo.SetActive(false);       
        });

        dynamicLayout.loadMoreEvent.AddListener(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                m_DataList.Add(m_DataList.Count);
            }
            dynamicLayout.RefreshCurrentItem();
        });

        dynamicLayout.SetAdapter(this);

        StartCoroutine(GenerateMsg());
    }

    void ShowText()
    {
        headText.text = string.Format("新加数据{0}条", m_TmpDataList.Count);
    }

    IEnumerator GenerateMsg()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(5);

            int index = m_DataList.Count + m_TmpDataList.Count;
            if (dynamicLayout.isLock)
            {
                m_TmpDataList.Add(index);
                headGo.SetActive(true);
                ShowText();
            }
            else
            {
                m_DataList.Add(index);
                dynamicLayout.RefreshAllItem();
            }
        }    
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            dynamicLayout.ScrollToItem(itemIndex, false, useAnimation, factor);
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            for (int i = 0; i < m_DataList.Count; i++)
            {
                m_DataList[i] += 1;
            }
            dynamicLayout.RefreshAllItem();
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            for (int i = 0; i < m_DataList.Count; i++)
            {
                m_DataList[i] += 1;
            }
            dynamicLayout.RefreshCurrentItem();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            m_DataList[itemIndex] += 10; 
            dynamicLayout.RefreshItem(itemIndex);
        }
    }

    public int GetCount()
    {
        return m_DataList.Count;
    }

    public bool IsEmpty()
    {
        return m_DataList.Count == 0;
    }

    public Vector2 GetItemSize()
    {
        return new Vector2(itemGo.rect.width, itemGo.rect.height);
    }

    public IItemView GetItemView(RectTransform itemParent)
    {
        var go = GameObject.Instantiate(itemGo.gameObject, itemParent);
        var itemView = new ItemView();
        itemView.rectTransform = go.transform as RectTransform; ;
        itemView.dataText = go.transform.GetChild(0).GetComponent<Text>();
        return itemView;
    }

    public void ProcessItemView(int position, IItemView itemView, DynamicLayout parent)
    {
        var data = m_DataList[position];
        var itemUI = itemView as ItemView;
        itemUI.dataText.text = string.Format("第{0}条数据", data);
    }

    public bool RecycleItemView(IItemView itemView, DynamicLayout parent)
    {
        return false;
    }

    public void RecycleItemViewDone(DynamicLayout parent)
    {
     
    }
}
