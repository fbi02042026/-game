// 自动生成，勿手改。用于探测 Unity 当前是否还能解析这些悬空 guid。
// 生成时间 2026-09-16
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public static class DanglingGuidProbe
{
    static readonly string[] Guids =
    {
        "027c6df94a8592b43964911bbf53ba7e",
        "0319aeb6b9d04b946bfada22f7e424ce",
        "05172d466879c2543afca06097d24f54",
        "090e1c7b819f3324893f067082340234",
        "0fdaf94cb374dd646b4996abe7921052",
        "1276863bcb10bd948b73f8373495b293",
        "1d393d1fe4206de41b971f4c09db7f0d",
        "20e0157fc1c7561439853e342ae6003b",
        "26aec5fbe2c73664caff74f8892c87b6",
        "27640c46f267de44498cdb914ad9f8b0",
        "29487a90692d7094bb54a2cd8b82f8f0",
        "2aad7e86c38a947479f21d635f1a5812",
        "2c76fb5aa1c71914eb76c2e952374e8e",
        "2f4fc4ab6d63aaa48a4e10d81b7c7680",
        "2f81cf3243b90da4ba28b7981ba9dd69",
        "30d487661a2678c48a4f6db25ea1cd17",
        "387ac9b4b7533c04d87797833758525b",
        "3db04a16d368d3544a75e9f1dc076a6f",
        "430bd0d6b082a0448aa5411680785e23",
        "45ffd3010b05a1a46a8470880cbafb6c",
        "4d82dbd035dae4d4bbb779534b69f68f",
        "518f4cce53c1ad04b91a2637ed746ee6",
        "554d9131c604082458f341a199f94326",
        "5fb04767ffe00e44998eedfd7e200474",
        "602b128d185715448b47b3c67dbde94a",
        "6350c1c1d19343243aafc10a3a47780b",
        "64363527937ad664b835674e026d620d",
        "6558a0508c8614a47bfe4b054233912f",
        "6a5739d3367d64146a264e9d1e727b91",
        "6a8d85a9ecd97924597caa40185e41b8",
        "6dfb85e55ccc7494283dab763e1ae787",
        "7039da37b4c452541838b17a53f1a9c4",
        "798fd5eda718a09479b9aa3ed7d799e1",
        "7b9a3c75b1b331e4d9011dce3a594f5e",
        "7c06b319a3e9b7c44af835113c6663a3",
        "81073cf40c6d23f4aa131d031dc59a00",
        "83ac7766c0f15004696bc4239563b3ff",
        "8baf16d1a1692a14589166ad9c0b57be",
        "8d5e009b8b83b1643bbfb27ad9065eed",
        "8e54f89fcf9ccbc468bb79d353bd91cb",
        "9060e15ffa3bd5841aa2307ee4a3b380",
        "9832842298c0ccb41aaab049dac53490",
        "9839559fab0b7c74f9de468093c0956d",
        "992bf9b445656d84aa72fba0b230b2ee",
        "9c54efdf5c3e03d489265d31be6000f8",
        "9d5da5470421f24409679f3ec3419cd7",
        "a1a71ef7599190b4d910a5c537c05845",
        "a33daf5bb6b93714083fe44405b948d2",
        "aa003d0f03c992545ac7d06d320ab165",
        "aa4ceb58594512549ba01ff0210f0c0b",
        "ae5068f0ab87a1c498727534c421b9f4",
        "b86d1ec10d1e85f419e1efbea5a6d0b5",
        "bf9b25478854ea94a9a065a30cb14da3",
        "c3e5a4331868107489ed223ff559719a",
        "c504e6bf9cb074c4786bc308a0e8c13e",
        "d04831f79b0535446a671ebc442dd1db",
        "d325162e92d4ea44ab91b6ce59d0051f",
        "da022ebf18601a14db5eee8502a7bdf9",
        "e1a8d61cd8a47d842952993f25d0296d",
        "e4aa6af24c8b5d944bfc09e0b30d5b03",
        "e72524dade4520349b4d5956b2fc7b9b",
        "e8def4a0e81a5d041ab7af3942f36d18",
        "ea6d622f895205543b3dce647d457da6",
        "ebf8be9ef02423d4e949b7e1f785a675",
        "f072664b2ff63a44394b7e9bfd15d28e",
        "fa2518063812ce642a8f861fff6f4c52",
        "faa6bfd410f35d0429bd1a3d0220bd59",
        "fdc2db57c3db7f748a6ec68f6fefbaf6",
        "fe48c01390e68c0418c511ce4dce7b81",
    };

    // prefab 路径 | 节点路径 | 悬空 guid
    static readonly string[][] Nodes =
    {
        new[] { "Assets/Resources/Prefabs/Town/AdventureLogUI.prefab", "AdventureLogUI/Root/Frame/Paper1/怪物/iconbg/普通边框", "7c06b319a3e9b7c44af835113c6663a3" },
        new[] { "Assets/Resources/Prefabs/Town/AdventureLogUI.prefab", "AdventureLogUI/Root/Frame/Paper1/怪物/iconbg/boss边框", "0319aeb6b9d04b946bfada22f7e424ce" },
        new[] { "Assets/Resources/Prefabs/Town/CharacterUI.prefab", "CharacterUI/Content/Stage/Portrait", "64363527937ad664b835674e026d620d" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card2/Skill1icon", "da022ebf18601a14db5eee8502a7bdf9" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card1/Role", "ebf8be9ef02423d4e949b7e1f785a675" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card1/Skill1icon", "da022ebf18601a14db5eee8502a7bdf9" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card0/Skill1icon", "da022ebf18601a14db5eee8502a7bdf9" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card0/Role", "ebf8be9ef02423d4e949b7e1f785a675" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card2/Role", "ebf8be9ef02423d4e949b7e1f785a675" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card0/Skill2icon", "da022ebf18601a14db5eee8502a7bdf9" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card0/ConfirmButton1", "c3e5a4331868107489ed223ff559719a" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card1/Skill2icon", "da022ebf18601a14db5eee8502a7bdf9" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card2/Skill2icon", "da022ebf18601a14db5eee8502a7bdf9" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card1/ConfirmButton", "2aad7e86c38a947479f21d635f1a5812" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card0/ConfirmButton", "2aad7e86c38a947479f21d635f1a5812" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/AutoRefresh/Image", "83ac7766c0f15004696bc4239563b3ff" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card0", "ea6d622f895205543b3dce647d457da6" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card2/ConfirmButton1", "c3e5a4331868107489ed223ff559719a" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card2/ConfirmButton", "2aad7e86c38a947479f21d635f1a5812" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card1/ConfirmButton1", "c3e5a4331868107489ed223ff559719a" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card2", "992bf9b445656d84aa72fba0b230b2ee" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel", "9060e15ffa3bd5841aa2307ee4a3b380" },
        new[] { "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab", "MercenaryRecruitPopup/Root/Frame/Panel/Card1", "602b128d185715448b47b3c67dbde94a" },
        new[] { "Assets/Resources/Prefabs/Town/PlayerNamingUI.prefab", "PlayerNamingUI/Root/Panel/ConfirmBtn", "430bd0d6b082a0448aa5411680785e23" },
        new[] { "Assets/Resources/Prefabs/Town/PlayerNamingUI.prefab", "PlayerNamingUI/Root/Panel/InputBar", "2f81cf3243b90da4ba28b7981ba9dd69" },
        new[] { "Assets/Resources/Prefabs/Town/PlayerNamingUI.prefab", "PlayerNamingUI/Root/Panel", "8e54f89fcf9ccbc468bb79d353bd91cb" },
        new[] { "Assets/Resources/Prefabs/Town/PlayerNamingUI.prefab", "PlayerNamingUI/Root/Panel/InputBar/DiceBtn", "0fdaf94cb374dd646b4996abe7921052" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/MercSkillAutoRow/Icon", "8d5e009b8b83b1643bbfb27ad9065eed" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/MusicRow/Icon", "2f4fc4ab6d63aaa48a4e10d81b7c7680" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/PrimaryButton", "6a8d85a9ecd97924597caa40185e41b8" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/MusicRow/Toggle/Knob", "aa4ceb58594512549ba01ff0210f0c0b" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/ConfirmButton", "5fb04767ffe00e44998eedfd7e200474" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Image (1)", "ae5068f0ab87a1c498727534c421b9f4" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/SfxRow/Toggle", "05172d466879c2543afca06097d24f54" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel", "9060e15ffa3bd5841aa2307ee4a3b380" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/EvacuateButton", "26aec5fbe2c73664caff74f8892c87b6" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/SfxRow/Icon", "d325162e92d4ea44ab91b6ce59d0051f" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/MercSkillAutoRow/Toggle", "05172d466879c2543afca06097d24f54" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/MusicRow/Toggle", "05172d466879c2543afca06097d24f54" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/MercSkillAutoRow/Toggle/Knob", "aa4ceb58594512549ba01ff0210f0c0b" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Content/ToggleList/SfxRow/Toggle/Knob", "aa4ceb58594512549ba01ff0210f0c0b" },
        new[] { "Assets/Resources/Prefabs/UI/SettingsPopup.prefab", "SettingsPopup/Root/Panel/Image", "ae5068f0ab87a1c498727534c421b9f4" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0", "6350c1c1d19343243aafc10a3a47780b" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/tuijian", "a33daf5bb6b93714083fe44405b948d2" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Stats/血量", "9c54efdf5c3e03d489265d31be6000f8" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/满星2/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/tuijian", "a33daf5bb6b93714083fe44405b948d2" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/满星2/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Stats/操控", "7b9a3c75b1b331e4d9011dce3a594f5e" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/满星3/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/满星1/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Icon", "518f4cce53c1ad04b91a2637ed746ee6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/满星2", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/满星1", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Stats/操控", "7b9a3c75b1b331e4d9011dce3a594f5e" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/满星2/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/满星3", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Stats/攻击", "6a5739d3367d64146a264e9d1e727b91" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/满星1/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Icon", "20e0157fc1c7561439853e342ae6003b" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Stats/操控", "7b9a3c75b1b331e4d9011dce3a594f5e" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/tuijian", "a33daf5bb6b93714083fe44405b948d2" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/满星3", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Stats/攻击", "6a5739d3367d64146a264e9d1e727b91" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Icon", "6dfb85e55ccc7494283dab763e1ae787" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel", "d04831f79b0535446a671ebc442dd1db" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/满星3/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/满星3", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/满星1", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Stats/血量", "9c54efdf5c3e03d489265d31be6000f8" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Stats/血量", "9c54efdf5c3e03d489265d31be6000f8" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2", "6350c1c1d19343243aafc10a3a47780b" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/EnterRiftBtn", "fa2518063812ce642a8f861fff6f4c52" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/满星2", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Stats/攻击", "6a5739d3367d64146a264e9d1e727b91" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/满星1/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard0/Rating/满星2", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/RefreshBtn", "bf9b25478854ea94a9a065a30cb14da3" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1/Rating/满星3/空星", "fdc2db57c3db7f748a6ec68f6fefbaf6" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard2/Rating/满星1", "81073cf40c6d23f4aa131d031dc59a00" },
        new[] { "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab", "PlayerJobSelect/Panel/CardRow/JobCard1", "6350c1c1d19343243aafc10a3a47780b" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_3/Lock", "e8def4a0e81a5d041ab7af3942f36d18" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_7/Highlight", "e4aa6af24c8b5d944bfc09e0b30d5b03" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_6/Region_6", "faa6bfd410f35d0429bd1a3d0220bd59" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_4/Region_4", "554d9131c604082458f341a199f94326" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_7/Lock", "e4aa6af24c8b5d944bfc09e0b30d5b03" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_1/Region_1", "387ac9b4b7533c04d87797833758525b" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_4/Lock", "554d9131c604082458f341a199f94326" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_6/Highlight", "faa6bfd410f35d0429bd1a3d0220bd59" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_8/Lock", "fe48c01390e68c0418c511ce4dce7b81" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_6/Lock", "faa6bfd410f35d0429bd1a3d0220bd59" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_5/Region_5", "9d5da5470421f24409679f3ec3419cd7" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_2/Lock", "1d393d1fe4206de41b971f4c09db7f0d" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_5/Lock", "9d5da5470421f24409679f3ec3419cd7" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_2/Highlight", "1d393d1fe4206de41b971f4c09db7f0d" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_3/Highlight", "e8def4a0e81a5d041ab7af3942f36d18" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_8/Region_8", "fe48c01390e68c0418c511ce4dce7b81" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_5/Highlight", "9d5da5470421f24409679f3ec3419cd7" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_1/Lock", "387ac9b4b7533c04d87797833758525b" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_2/Region_2", "1d393d1fe4206de41b971f4c09db7f0d" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_1/Highlight", "387ac9b4b7533c04d87797833758525b" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_4/Highlight", "554d9131c604082458f341a199f94326" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_8/Highlight", "fe48c01390e68c0418c511ce4dce7b81" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_7/Region_7", "e4aa6af24c8b5d944bfc09e0b30d5b03" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/MapBackground/land", "798fd5eda718a09479b9aa3ed7d799e1" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/MapBackground", "3db04a16d368d3544a75e9f1dc076a6f" },
        new[] { "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab", "WorldMapPopup/Regions/Region_3/Region_3", "e8def4a0e81a5d041ab7af3942f36d18" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel", "a1a71ef7599190b4d910a5c537c05845" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/StatsPanel/StatRow_Crit/Icon", "f072664b2ff63a44394b7e9bfd15d28e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/StatsPanel/StatRow_Heal/Icon", "4d82dbd035dae4d4bbb779534b69f68f" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/StatsPanel/StatRow_Taken/Icon", "27640c46f267de44498cdb914ad9f8b0" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/StatsPanel/StatRow_Combo/Icon", "8baf16d1a1692a14589166ad9c0b57be" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/mask/PortraitHost", "e1a8d61cd8a47d842952993f25d0296d" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/StatsPanel/StatRow_Damage/Icon", "30d487661a2678c48a4f6db25ea1cd17" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab", "BattleSettlement/Root/Panel/StatsPanel/StatRow_Kill/Icon", "9839559fab0b7c74f9de468093c0956d" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot2/PlayerSlot", "027c6df94a8592b43964911bbf53ba7e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BossBar/boss", "1276863bcb10bd948b73f8373495b293" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BossBar/血条", "6558a0508c8614a47bfe4b054233912f" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot1/xuetiaodi", "2c76fb5aa1c71914eb76c2e952374e8e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot2/xuetiaodi/职业icon", "ebf8be9ef02423d4e949b7e1f785a675" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot2/xuetiaodi", "2c76fb5aa1c71914eb76c2e952374e8e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/SkillBar/icon底3", "7039da37b4c452541838b17a53f1a9c4" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/zhuangbei/bg", "b86d1ec10d1e85f419e1efbea5a6d0b5" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/PlayerSlot/xuetiaodi/职业icon", "ebf8be9ef02423d4e949b7e1f785a675" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/SkillBar/icon底0", "7039da37b4c452541838b17a53f1a9c4" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/SkillBar/icon底1", "7039da37b4c452541838b17a53f1a9c4" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot1/PlayerSlot/Portrait", "c504e6bf9cb074c4786bc308a0e8c13e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/PlayerSlot", "027c6df94a8592b43964911bbf53ba7e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/PlayerSlot/Portrait", "c504e6bf9cb074c4786bc308a0e8c13e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot1/xuetiaodi/职业icon", "ebf8be9ef02423d4e949b7e1f785a675" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot1/PlayerSlot", "027c6df94a8592b43964911bbf53ba7e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/PlayerSlot/xuetiaodi", "2c76fb5aa1c71914eb76c2e952374e8e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/CharacterBar/MercSlot2/PlayerSlot/Portrait", "c504e6bf9cb074c4786bc308a0e8c13e" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/zhezhao/Image (1)", "9832842298c0ccb41aaab049dac53490" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BossBar/di", "aa003d0f03c992545ac7d06d320ab165" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/GridContainer/bg", "45ffd3010b05a1a46a8470880cbafb6c" },
        new[] { "Assets/Resources/Prefabs/Battle/BattleUI.prefab", "BattleUI/BackpackPanel/SkillBar/icon底2", "7039da37b4c452541838b17a53f1a9c4" },
        new[] { "Assets/Resources/Prefabs/Battle/NextStageRoulette.prefab", "NextStageRoulette/Shade", "29487a90692d7094bb54a2cd8b82f8f0" },
        new[] { "Assets/Resources/Prefabs/Dialogue/DialogueUI.prefab", "DialogueUI/LeftPortrait", "e72524dade4520349b4d5956b2fc7b9b" },
        new[] { "Assets/Resources/Prefabs/Talent/TalentUI.prefab", "TalentUI/Panel/ResourceRow/StonePlus", "090e1c7b819f3324893f067082340234" },
    };

    [MenuItem("Tools/诊断/探针：悬空 GUID 当前是否可解析")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== 1. AssetDatabase.GUIDToAssetPath 结果 ===");
        sb.AppendLine("DEAD = Unity 也不认；OK = Unity 认，后面是它解析出的路径");
        foreach (var g in Guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(p))
            {
                sb.AppendLine("DEAD  " + g);
            }
            else
            {
                sb.AppendLine("OK    " + g + "  ->  " + p);
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== 2. 每个悬空节点当前 Image 上到底有没有图 ===");
        sb.AppendLine("SPRITE=xxx = 有图；SPRITE-NULL = 没图（说明运行时会被代码赋值）");
        foreach (var row in Nodes)
        {
            string prefabPath = row[0];
            string nodePath = row[1];
            string guid = row[2];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                sb.AppendLine("LOAD-FAIL  " + prefabPath);
                continue;
            }
            Transform t = prefab.transform;
            if (!string.IsNullOrEmpty(nodePath))
            {
                t = prefab.transform.Find(nodePath);
            }
            if (t == null)
            {
                sb.AppendLine("NO-NODE  " + prefabPath + "  |  " + nodePath);
                continue;
            }
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                sb.AppendLine("NO-IMAGE  " + prefabPath + "  |  " + nodePath);
                continue;
            }
            string state = img.sprite == null ? "SPRITE-NULL" : ("SPRITE=" + img.sprite.name);
            sb.AppendLine(state + "  |  " + prefabPath + "  |  " + nodePath + "  |  " + guid);
        }

        string outPath = Path.Combine(Application.dataPath, "../Temp/dangling_probe_result.txt");
        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        Debug.Log("[DanglingGuidProbe] 结果已写入: " + outPath);
        Debug.Log(sb.ToString());
    }
}
