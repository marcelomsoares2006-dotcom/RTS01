#include <filesystem>
#include <iostream>
#include <vector>

int main() {
    const std::vector<int> weights{1, 2, 3};
    std::cout << "MSVC C++23 OK; entries=" << weights.size()
              << "; cwd=" << std::filesystem::current_path().string() << '\n';
    return weights.size() == 3 ? 0 : 1;
}
