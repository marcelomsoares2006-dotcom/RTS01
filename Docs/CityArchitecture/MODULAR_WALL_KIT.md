# Kit modular de muralha

## Arquivos

O gerador está em `Assets/Editor/CityArchitecture/ModularWallKitBuilder.cs`. Ele cria, sem alterar prefabs existentes:

- `Assets/CityArchitecture/Prefabs/WallStraight.prefab`
- `Assets/CityArchitecture/Prefabs/WallCorner.prefab`
- `Assets/CityArchitecture/Prefabs/Gatehouse.prefab`
- `Assets/CityArchitecture/Prefabs/WallTowerConnector.prefab`
- materiais em `Assets/CityArchitecture/Materials/`

Cada prefab contém quatro variantes filhas: `Ancient`, `Classical`, `Medieval` e `Imperial`. Os módulos são visuais e não adicionam colisores, para não interferir no placement ou NavMesh do projeto atual.

## Geração no Unity

1. Feche o Play Mode.
2. Abra `Tools > City Architecture > Build Modular Wall Kit`.
3. Aguarde o `AssetDatabase.Refresh`.
4. Arraste a variante de era desejada para uma cena de teste.
5. Para integração jogável, crie um prefab de gameplay separado e adicione colisão/NavMesh de forma explícita; não use este kit diretamente como `BuildingData` sem teste de placement.

## Convenção de encaixe

- `WallStraight`: comprimento nominal de 6 unidades.
- `WallCorner`: braços nominais de 5 unidades.
- `Gatehouse`: largura aproximada de 6,1 unidades.
- `WallTowerConnector`: centro da torre na origem; conectores nos eixos X e Z.

## Auditoria e preservação

O script não referencia `BuildingEraDecoration`, não abre nem salva cenas e não edita prefabs existentes. Antes de catalogar como construção jogável, validar escala, pivô, câmera RTS, colisão, placement, NavMesh e save/load.
